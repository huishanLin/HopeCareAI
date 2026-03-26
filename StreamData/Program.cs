// See https://aka.ms/new-console-template for more information
using Dapper;
using Inference.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog;
using StreamData.Models;
using System.Net.Http.Json;
using System.Text.Json;

// 1. 初始化 Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // 設定最小日誌層級
    .WriteTo.Console()    // 輸出到控制台
    .WriteTo.File("logs/stream.txt", rollingInterval: RollingInterval.Day) // 輸出到檔案
    .CreateLogger();

// 建立組態物件，讀取 appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var stream_apiUrl = config.GetSection("StreamApiUrl").Value;
var _connectString_hope_care = config.GetConnectionString("hope_care");
int intervalMinutes = config.GetValue<int>("IntervalMinutes", 5); // 每次抓取的時間區間（分鐘）
var _nikkisoMappings = config.GetSection("FACTORS:NIKKISO").GetChildren()
                      .ToDictionary(x => x.Key, x => x.Value);

try
{
    #region SQL Script
    /* 取得當日透析中病患的最新透析資料，以及確認的處方資料。
     * 血壓、脈搏等生理參數若無法從 hcthdgdata 取得，則嘗試從 hcdrecord 取得最新一筆有值的記錄。
     */
    var selectSql = @"
                WITH RankedList AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
                    FROM dbo.hcmhdlist
                    where rcdstatus between '2' and '4' and hddate::date = @hddate
                ),
                LatestPlan AS (
                    SELECT *
                    FROM dbo.hcdplanrcd
                    where hddate::date = @hddate
                ),
                LatestData AS (
                    SELECT h.*, n.patientid , ROW_NUMBER() OVER (PARTITION BY h.nfcno ORDER BY hddate DESC) AS rn
                    FROM dbo.hcthdgdata h inner join dbo.nfc n on n.nfcno = h.nfcno 
                    where h.startflag = '1'
                ),
                LatestRecord as (
	                SELECT DISTINCT ON (h.hdid)
	                    h.hdid,h.systolic,h.diastolic, h.pulse, h.hddate
	                FROM dbo.hcdrecord h
	                WHERE h.systolic IS NOT NULL AND h.diastolic IS NOT NULL AND h.hddate::date = @hddate
	                ORDER BY h.hdid, h.hddate DESC
                )                   
                SELECT 
                    d.machineid AS machineid,
                    list.id AS hd_id,
	                list.chartid AS patient_uuid,
                    --CURRENT_TIMESTAMP AS transfer_time,
	                d.hddate AS transfer_time,
                    list.bedno AS bed_name,
	                list.startdt AS start_time,
	                list.enddt AS end_time,
	                coalesce(d.systolic, r.systolic) AS bps,
                    coalesce(d.diastolic, r.diastolic) AS bpd,
                    coalesce(d.pulse, r.pulse) AS hr,
                    coalesce(d.pulse,r.pulse) AS pulse,
	                p.dryweight AS dry_weight,
                    -- 200 AS arterial_blood_flow,
	                d.bloodflow AS arterial_blood_flow,
                    ---3.57 AS arterial_pressure,
	                --d.ap AS arterial_pressure,
	                --77.96 AS bi_bag_conductivity,
	                --23.19 AS bi_bag_temperature,
	                --0.17 AS bicarbonate_adjustment,
                    --243.21 AS blood_flow_rate,
	                --d.bloodflow AS blood_flow_rate,
	                d.bodytemperature AS blood_temperature,
  	                --135.33 AS cyclic_Pressure_Holding_Test,
                    --14.18 AS dialysate_conductivity,
	                d.liqconct AS dialysate_conductivity,
	                d.liqtemp AS dialysate_temperature,
                    --468.81 AS dialysisate_flow_rate,
	                d.liqflow AS dialysisate_flow_rate,
	                --4681.16 AS heparin_accumulated_bolus_volume,
	                --4232.01 AS heparin_accumulated_volume,
	                d.anticglflow AS heparin_delivery_rate,
                    --351.49 AS target_arterial_blood_flow,
	                d.bloodflow, 220 target_arterial_blood_flow,
	                NULLIF(REGEXP_REPLACE(p.hdliqna, '[^0-9.]', '', 'g'), '')::numeric AS target_sodium,
	                d.ufgoal AS target_uf,
	                d.tmp AS tmp,
                    --1009.44 AS total_uf,
	                d.ufvolumn AS total_uf,
                    --90.24 AS uf_rate,
	                d.ufrate AS uf_rate,
                    --59.66 AS venous_pressure
	                d.vp AS venous_pressure
                FROM RankedList AS list
                INNER JOIN LatestData AS d ON list.patientid = d.patientid and d.rn <= @intervalMinutes
                INNER JOIN LatestPlan AS p ON list.id = p.hdid 
                LEFT JOIN LatestRecord AS r ON list.id = r.hdid
                WHERE list.rn = 1";
    #endregion

    using var sourceConn = new NpgsqlConnection(_connectString_hope_care);
    Log.Information($"sql:{selectSql}");
    var result = await sourceConn.QueryAsync<StreamDto>(selectSql, new { hddate = DateTime.Today, intervalMinutes });

    if (result.Count() == 0)
    {
        Log.Information("無資料需要上傳");
        return;
    }
    else
    {
        // 資料要進行 factor 計算
        foreach (var item in result)
        {
            if(!string.IsNullOrEmpty(item.machineid) && item.machineid.ToUpper().StartsWith("NIKKISO"))
            {
                foreach (var property in item.GetType().GetProperties())
                { 
                    if (_nikkisoMappings.TryGetValue(property.Name, out string factor))
                    {
                        var originalValue = (float)property.GetValue(item);
                        var calculatedValue = CalculateWithFactor(originalValue, factor);
                        property.SetValue(item, calculatedValue);
                        Log.Information($"已對 NIKKISO 的 {property.Name} 進行 factor 計算，原值: {originalValue}, 計算後: {calculatedValue}");
                    }
                }
                
            }
        }
    }

    try
    {
        // 將 batch 以 JSON 格式 POST 到指定 URL
        using var httpClient = new HttpClient();
        var jsonString = JsonSerializer.Serialize(result); // 👈 序列化後就能看到內容
        Log.Information("Request Body:\n" + jsonString);
        var response = await httpClient.PostAsJsonAsync(stream_apiUrl, result);

        if (response.IsSuccessStatusCode)
        {
            var resultData = await response.Content.ReadAsStringAsync();
            //logger.Info($"本批次實際處理 {batch.Count} 筆，POST 成功");
            Log.Information($"POST 成功，共處理 {result.AsList().Count} 筆資料。回應內容:{resultData}");
            await SavePredictionResult(_connectString_hope_care, resultData);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            //logger.Info($"POST 失敗: {response.StatusCode} - {error}");
            //break;
            Log.Information($"POST 失敗: {response.StatusCode} - {error}");
        }
    }
    catch (Exception ex)
    {
        //logger.Info($"批次處理失敗 (offset={offset}, count={batch.Count}): {ex.Message}");
        //// 可視情況決定是否 break、continue 或重試
        //break; // 或 continue;
        Log.Information($"批次處理失敗: {ex.Message}");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "發生未預期的錯誤");
}
finally
{
    Log.CloseAndFlush();
}


partial class Program
{
    // 這裡可以放一些共用的方法或屬性
    private static async Task SavePredictionResult(string _connectString_hope_care, string data)
    {
        // 實作將預測結果儲存到資料庫的邏輯
        // 例如使用 Dapper 執行 INSERT 語句
        List<HopeCareAIPredictions> lstAIPredictions = new List<HopeCareAIPredictions>();
        var resultData = JsonSerializer.Deserialize<List<Dictionary<string, InferencePredictionModel>>>(data);

        if (resultData != null)
        {
            foreach (var dict in resultData)
            {
                foreach (var entry in dict)
                {

                    var patientuuid = entry.Key; // 這裡拿到 "6297"
                    var content = entry.Value.prediction;

                    Log.Information($"處理患者 ID: {patientuuid}");

                    // 2. 遍歷模型索引 (0, 1, 2, 3)
                    foreach (var index in content.model.Keys)
                    {
                        string modelName = content.model[index];
                        double? predictValue = content.predict[index];
                        double? prob = content.probability[index];
                        double? threshold = content.threshold[index];

                        Log.Information($"- model: {modelName}");
                        Log.Information($"  predict: {predictValue?.ToString() ?? "null"}");
                        Log.Information($"  probability: {prob?.ToString() ?? "null"}");
                        Log.Information($"  threshold: {threshold?.ToString() ?? "null"}");

                        var aiPrediction = new HopeCareAIPredictions()
                        {
                            Patientuuid = patientuuid,
                            Category = modelName,
                            Result = predictValue?.ToString(),
                            Probability = prob?.ToString(),
                            Threshold = threshold?.ToString()
                        };

                        lstAIPredictions.Add(aiPrediction);
                    }
                }
            }
            if (lstAIPredictions.Count > 0)
            {
                try
                {
                    using var sourceConn = new NpgsqlConnection(_connectString_hope_care);
                    var insertSQL = @"Insert into dbo.aipredictions
                                        (patientuuid, category, result, probability, threshold, predictiondate)
                                        values
                                        (@Patientuuid, @Category, @Result, @Probability, @Threshold, CURRENT_TIMESTAMP)";
                    var executeCount = await sourceConn.ExecuteAsync(insertSQL, lstAIPredictions);
                    Log.Information($"已新增 {executeCount} 筆 AI 預測結果至資料庫。");
                }
                catch (Exception ex)
                {

                    Log.Error(ex, "儲存 AI 預測結果時發生錯誤");
                }
            }
        }
    }

    /// <summary>
    /// 原數值要再加入計算 factor 的數值
    /// </summary>
    /// <param name="originalValue">原來的數值</param>
    /// <param name="factor">factor 的格式： /100 => 除以 100, *10 => 乘以10</param>
    /// <returns>加入 factor 後的結果</returns>
    private static float CalculateWithFactor(float originalValue, string factor)
    {
        var op = factor.Substring(0, 1);
        var sValue = factor.Substring(1, factor.Length - 1);
        if (!float.TryParse(sValue, out float value)) return originalValue;
        switch (op)
        {
            case "/":
                return originalValue / value;
            case "*":
                return originalValue * value;
            default:
                return originalValue;
        }
    }
}
