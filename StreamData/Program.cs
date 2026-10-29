// See https://aka.ms/new-console-template for more information
using Dapper;
using Inference.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog;
using StreamData.Models;
using System.Net.Http.Json;
using System.Numerics;
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
string intervalTime = DateTime.Now.AddMinutes(-intervalMinutes).ToString("yyyy-MM-dd HH:mm");
var version = config.GetSection("Version").Value;
var _nikkisoMappings = config.GetSection($"FACTORS_{version}:NIKKISO").GetChildren()
                      .ToDictionary(x => x.Key, x => x.Value);

try
{
    //#region SQL Script
    /* 取得當日透析中病患的最新透析資料，以及確認的處方資料。
     * 血壓、脈搏等生理參數若無法從 hcthdgdata 取得，則嘗試從 hcdrecord 取得最新一筆有值的記錄。
     */
    //var selectSql = @"
    //            WITH RankedList AS (
    //                SELECT *, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
    //                FROM dbo.hcmhdlist
    //                where rcdstatus between '2' and '3' and hddate::date = @hddate
    //            ),
    //            LatestPlan AS (
    //                SELECT *
    //                FROM dbo.hcdplanrcd
    //                where hddate::date = @hddate
    //            ),
    //            LatestData AS (
    //                SELECT h.*, n.patientid , ROW_NUMBER() OVER (PARTITION BY h.nfcno ORDER BY hddate DESC) AS rn
    //                FROM dbo.hcthdgdata h inner join dbo.nfc n on n.nfcno = h.nfcno 
    //                where h.startflag = '1' and h.hddate >= @intervalTime::timestamptz
    //            ),
    //            LatestRecord as (
    //             SELECT DISTINCT ON (h.hdid)
    //                 h.hdid,h.systolic,h.diastolic, h.pulse, h.hddate
    //             FROM dbo.hcdrecord h
    //             WHERE h.systolic IS NOT NULL AND h.diastolic IS NOT NULL AND h.hddate::date = @hddate
    //             ORDER BY h.hdid, h.hddate DESC
    //            )                   
    //            SELECT 
    //                d.machineid AS machineid,
    //                list.id AS hd_id,
    //             list.chartid AS patient_uuid,
    //             d.hddate AS transfer_time,
    //                CONCAT('1HDU',list.bedno) AS bed_name,
    //             list.startdt AS start_time,
    //             list.enddt AS end_time,
    //             coalesce(d.systolic, r.systolic) AS bps,
    //                coalesce(d.diastolic, r.diastolic) AS bpd,
    //                coalesce(d.pulse, r.pulse) AS hr,
    //                coalesce(d.pulse,r.pulse) AS pulse,
    //             p.dryweight AS dry_weight,
    //             d.bloodflow AS arterial_blood_flow,
    //             d.bodytemperature AS blood_temperature,
    //             d.liqconct AS dialysate_conductivity,
    //             d.liqtemp AS dialysate_temperature,
    //             d.liqflow AS dialysisate_flow_rate,
    //             d.anticglflow AS heparin_delivery_rate,
    //             p.bloodflow as target_arterial_blood_flow,
    //             NULLIF(REGEXP_REPLACE(p.hdliqna, '[^0-9.]', '', 'g'), '')::numeric AS target_sodium,
    //             d.ufgoal AS target_uf,
    //             d.tmp AS tmp,
    //             d.ufvolumn AS total_uf,
    //             d.ufrate AS uf_rate,
    //             d.vp AS venous_pressure
    //            FROM RankedList AS list
    //            INNER JOIN LatestData AS d ON list.patientid = d.patientid and d.rn = 1
    //            INNER JOIN LatestPlan AS p ON list.id = p.hdid 
    //            LEFT JOIN LatestRecord AS r ON list.id = r.hdid
    //            WHERE list.rn = 1";
    //#endregion

    //using var sourceConn = new NpgsqlConnection(_connectString_hope_care);
    //Log.Information($"sql:{selectSql}");
    //var result = await sourceConn.QueryAsync<StreamDto>(selectSql, new { hddate = DateTime.Today, intervalTime });

    var result = await (version switch
    {
        "v1" => ProcData(_connectString_hope_care, intervalTime)
                    .ContinueWith(t => t.Result.Cast<IStreamData>()),
        "v2" => ProcDataV2(_connectString_hope_care, intervalTime)
                    .ContinueWith(t => t.Result.Cast<IStreamData>()),
        _ => Task.FromException<IEnumerable<IStreamData>>(
                new InvalidOperationException($"不支援的版本: {version}"))
    });

    if (!result.Any())
    {
        Log.Information("無資料需要上傳");
        return;
    }
    // 資料要進行 factor 計算
    foreach (var item in result)
    {
        if (!string.IsNullOrEmpty(item.MachineIdentifier) && item.MachineIdentifier.ToUpper().StartsWith("NIKKISO"))
        {
            foreach (var property in _nikkisoMappings)
            {
                var propertyInfo = item.GetType().GetProperty(property.Key);
                var originalValue = (float)propertyInfo.GetValue(item);
                var calculatedValue = CalculateWithFactor(originalValue, property.Value);
                propertyInfo.SetValue(item, calculatedValue);
                Log.Information($"已對 NIKKISO 的 {property.Key} 進行 factor 計算，原值: {originalValue}, 計算後: {calculatedValue}");
            }
            //foreach (var property in item.GetType().GetProperties())
            //{
            //    if (_nikkisoMappings.TryGetValue(property.Name, out string factor))
            //    {
            //        var originalValue = (float)property.GetValue(item);
            //        var calculatedValue = CalculateWithFactor(originalValue, factor);
            //        property.SetValue(item, calculatedValue);
            //        Log.Information($"已對 NIKKISO 的 {property.Name} 進行 factor 計算，原值: {originalValue}, 計算後: {calculatedValue}");
            //    }
            //}
        }
    }
    

    try
    {
        // 將 batch 以 JSON 格式 POST 到指定 URL
        using var httpClient = new HttpClient();
        var jsonString = JsonSerializer.Serialize(result.Select(x => (object)x), new JsonSerializerOptions
        {
            WriteIndented = true
        }); // 👈 序列化後就能看到內容
        Log.Information("Request Body:\n" + jsonString);
        var response = await httpClient.PostAsJsonAsync(stream_apiUrl, result.Select(x=> (object)x));

        if (response.IsSuccessStatusCode)
        {
            var resultData = await response.Content.ReadAsStringAsync();
            //logger.Info($"本批次實際處理 {batch.Count} 筆，POST 成功");
            Log.Information($"POST 成功，共處理 {result.AsList().Count} 筆資料。回應內容:{resultData}");

            await (version switch
            {
                "v1" => SavePredictionResult(_connectString_hope_care, resultData),
                "v2" => SavePredictionResultV2(_connectString_hope_care, resultData),
                _ => Task.FromException(new InvalidOperationException($"不支援的版本: {version}"))
            });
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

    private static async Task<IEnumerable<StreamDto>> ProcData(string connectString_hope_care, string intervalTime)
    {
        #region SQL Script
        /* 取得當日透析中病患的最新透析資料，以及確認的處方資料。
         * 血壓、脈搏等生理參數若無法從 hcthdgdata 取得，則嘗試從 hcdrecord 取得最新一筆有值的記錄。
         */
        var selectSql = @"
                WITH RankedList AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
                    FROM dbo.hcmhdlist
                    where rcdstatus between '2' and '3' and hddate::date = @hddate
                ),
                LatestPlan AS (
                    SELECT *
                    FROM dbo.hcdplanrcd
                    where hddate::date = @hddate
                ),
                LatestData AS (
                    SELECT h.*, n.patientid , ROW_NUMBER() OVER (PARTITION BY h.nfcno ORDER BY hddate DESC) AS rn
                    FROM dbo.hcthdgdata h inner join dbo.nfc n on n.nfcno = h.nfcno 
                    where h.startflag = '1' and h.hddate >= @intervalTime::timestamptz
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
	                d.hddate AS transfer_time,
                    CONCAT('1HDU',list.bedno) AS bed_name,
	                list.startdt AS start_time,
	                list.enddt AS end_time,
	                coalesce(d.systolic, r.systolic) AS bps,
                    coalesce(d.diastolic, r.diastolic) AS bpd,
                    coalesce(d.pulse, r.pulse) AS hr,
                    coalesce(d.pulse,r.pulse) AS pulse,
	                p.dryweight AS dry_weight,
	                d.bloodflow AS arterial_blood_flow,
	                d.bodytemperature AS blood_temperature,
	                d.liqconct AS dialysate_conductivity,
	                d.liqtemp AS dialysate_temperature,
	                d.liqflow AS dialysisate_flow_rate,
	                d.anticglflow AS heparin_delivery_rate,
	                p.bloodflow as target_arterial_blood_flow,
	                NULLIF(REGEXP_REPLACE(p.hdliqna, '[^0-9.]', '', 'g'), '')::numeric AS target_sodium,
	                d.ufgoal AS target_uf,
	                d.tmp AS tmp,
	                d.ufvolumn AS total_uf,
	                d.ufrate AS uf_rate,
	                d.vp AS venous_pressure
                FROM RankedList AS list
                INNER JOIN LatestData AS d ON list.patientid = d.patientid and d.rn = 1
                INNER JOIN LatestPlan AS p ON list.id = p.hdid 
                LEFT JOIN LatestRecord AS r ON list.id = r.hdid
                WHERE list.rn = 1";
        #endregion

        using var sourceConn = new NpgsqlConnection(connectString_hope_care);
        Log.Information($"sql:{selectSql}");
        var result = await sourceConn.QueryAsync<StreamDto>(selectSql, new { hddate = DateTime.Today, intervalTime });

        return result;
    }

    private static async Task<IEnumerable<StreamDtoV2>> ProcDataV2(string connectString_hope_care, string intervalTime)
    {
        #region SQL Script
        /* 取得當日透析中病患的最新透析資料，以及確認的處方資料。
         * 血壓、脈搏等生理參數若無法從 hcthdgdata 取得，則嘗試從 hcdrecord 取得最新一筆有值的記錄。
         */
        var selectSql = @"
                WITH RankedList AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
                    FROM dbo.hcmhdlist
                    where rcdstatus between '2' and '3' and hddate::date = @hddate
                ),
                LatestPlan AS (
                    SELECT hp.*, h.liqtemp 
                    FROM dbo.hcdplanrcd hp left join dbo.hdplan h on hp.hdplanid = h.id  
                    where hddate::date = @hddate
                ),
                LatestData AS (
                    SELECT h.*, n.patientid , ROW_NUMBER() OVER (PARTITION BY h.nfcno ORDER BY hddate DESC) AS rn
                    FROM dbo.hcthdgdata h inner join dbo.nfc n on n.nfcno = h.nfcno 
                    where h.startflag = '1' and h.hddate >= @intervalTime::timestamptz
                    and n.enddate is null
                ),
                LatestRecord as (
                    SELECT DISTINCT ON (h.hdid)
                        h.hdid,h.systolic,h.diastolic, h.pulse, h.hddate
                    FROM dbo.hcdrecord h
                    WHERE h.systolic IS NOT NULL AND h.diastolic IS NOT NULL AND h.hddate::date = @hddate
                    ORDER BY h.hdid, h.hddate DESC
                )                   
                SELECT 
                    list.id AS hd_id,
                    --CONCAT('2HDU',list.chartid) AS chartid,
                    list.chartid AS chartid,
                    d.hddate::date AS transfer_time,
                    --CONCAT('1HDU',list.bedno) AS bed_name,
                    list.bedno AS bed_name,
                    d.machineid AS machine_model,
                    d.bloodflow as blood_flow_rate,
                    d.ufrate AS uf_rate,
                    p.bloodflow as arterial_blood_flow,
                    d.ap as arterial_pressure,
                    d.AccBloodVol as blood_flow_volume,
                    coalesce(d.systolic, r.systolic) AS blood_pressure_systolic,
                    coalesce(d.diastolic, r.diastolic) AS blood_pressure_diastolic,
                    d.liqflow AS dialysisate_flow_rate,
                    coalesce(d.pulse,r.pulse) AS pulse_rate,
                    d.tmp AS transmembrane_pressure,
                    d.ufvolumn AS uf_volume,
                    d.vp AS venous_pressure,
                    d.liqconct AS dialysate_conductivity,
                    d.liqtemp AS dialysate_temperature,
                    d.anticglqty AS heparin_accumulated_volume,
                    d.anticglflow as heparin_delivery_rate,
                    NULLIF(REGEXP_REPLACE(p.hdliqna, '[^0-9.]', '', 'g'), '')::numeric AS target_sodium,
                    d.ufgoal AS target_uf,
                    d.uftime as uf_time,
                    p.bloodflow AS arterial_blood_flow_set,
                    p.liqtemp as dialysate_temperature_set,
                    p.liqflow as dialysate_flow_rate_set    
                FROM RankedList AS list
                INNER JOIN LatestData AS d ON list.patientid = d.patientid and d.rn = 1
                INNER JOIN LatestPlan AS p ON list.id = p.hdid 
                LEFT JOIN LatestRecord AS r ON list.id = r.hdid
                WHERE list.rn = 1;";
        #endregion

        using var sourceConn = new NpgsqlConnection(connectString_hope_care);
        Log.Information($"sql:{selectSql}");
        var result = await sourceConn.QueryAsync<StreamDtoV2>(selectSql, new { hddate = DateTime.Today, intervalTime });

        return result;
    }

    private static async Task SavePredictionResultV2(string _connectString_hope_care, string data)
    {
        //data = @"{""hgb"":[{""external_patient_id"":""1368754"",""date"":""2026-10-28"",""hgb_prediction"":8.442337598561851},{""external_patient_id"":""5821003"",""date"":""2026-10-28"",""hgb_prediction"":10.456317545134727},{""external_patient_id"":""1075322"",""date"":""2026-10-28"",""hgb_prediction"":10.612488137497744},{""external_patient_id"":""8365722"",""date"":""2026-10-28"",""hgb_prediction"":10.866417625245022},{""external_patient_id"":""2313736"",""date"":""2026-10-28"",""hgb_prediction"":10.498869431560502},{""external_patient_id"":""3547598"",""date"":""2026-10-28"",""hgb_prediction"":10.419230705365798},{""external_patient_id"":""6743088"",""date"":""2026-10-28"",""hgb_prediction"":10.918289334112206},{""external_patient_id"":""3325494"",""date"":""2026-10-28"",""hgb_prediction"":11.685003673544543}],""anemia"":[{""external_patient_id"":""1368754"",""date"":""2026-10-28"",""anemia_prediction"":1.0,""anemia_probability"":0.6014875923612207},{""external_patient_id"":""5821003"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.16714869054917514},{""external_patient_id"":""1075322"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.055512461899522705},{""external_patient_id"":""8365722"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.05318903429433949},{""external_patient_id"":""2313736"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.2650111506492173},{""external_patient_id"":""3547598"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.10070210932172115},{""external_patient_id"":""6743088"",""date"":""2026-10-28"",""anemia_prediction"":0.0,""anemia_probability"":0.08966848693799444},{""external_patient_id"":""3325494"",""date"":""2026-10-28"",""anemia_prediction"":1.0,""anemia_probability"":0.843187237772084}],""hf"":[{""external_patient_id"":""1368754"",""date"":""2026-10-28"",""hf_prediction_hospital"":0,""hf_probability"":0.24956421356421354,""hf_prediction_clinic"":0},{""external_patient_id"":""5821003"",""date"":""2026-10-28"",""hf_prediction_hospital"":0,""hf_probability"":0.3397420634920635,""hf_prediction_clinic"":0},{""external_patient_id"":""1075322"",""date"":""2026-10-28"",""hf_prediction_hospital"":1,""hf_probability"":0.48080952380952385,""hf_prediction_clinic"":0},{""external_patient_id"":""8365722"",""date"":""2026-10-28"",""hf_prediction_hospital"":0,""hf_probability"":0.2835198412698412,""hf_prediction_clinic"":0},{""external_patient_id"":""2313736"",""date"":""2026-10-28"",""hf_prediction_hospital"":1,""hf_probability"":0.45770634920634923,""hf_prediction_clinic"":0},{""external_patient_id"":""3547598"",""date"":""2026-10-28"",""hf_prediction_hospital"":0,""hf_probability"":0.2693928571428571,""hf_prediction_clinic"":0},{""external_patient_id"":""6743088"",""date"":""2026-10-28"",""hf_prediction_hospital"":1,""hf_probability"":0.45256878306878323,""hf_prediction_clinic"":0},{""external_patient_id"":""3325494"",""date"":""2026-10-28"",""hf_prediction_hospital"":1,""hf_probability"":0.3815674603174603,""hf_prediction_clinic"":0}],""idh"":[{""external_patient_id"":""1368754"",""date"":""2026-10-28"",""idh_probability"":0.12027355917934696,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":46.0,""ut_prediction"":6.0},{""external_patient_id"":""5821003"",""date"":""2026-10-28"",""idh_probability"":0.09017256139776031,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":28.200000000000003,""ut_prediction"":6.0},{""external_patient_id"":""1075322"",""date"":""2026-10-28"",""idh_probability"":0.014423361943832771,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":79.8,""ut_prediction"":6.0},{""external_patient_id"":""8365722"",""date"":""2026-10-28"",""idh_probability"":0.1524924808849966,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":61.400000000000006,""ut_prediction"":6.0},{""external_patient_id"":""2313736"",""date"":""2026-10-28"",""idh_probability"":0.04719957768024315,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":69.4,""ut_prediction"":4.5},{""external_patient_id"":""3547598"",""date"":""2026-10-28"",""idh_probability"":0.10130152182654724,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":51.5,""ut_prediction"":6.0},{""external_patient_id"":""6743088"",""date"":""2026-10-28"",""idh_probability"":0.03441791221948426,""idh_prediction"":0.0,""recommendation"":""維持常規監控"",""dw_prediction"":79.4,""ut_prediction"":6.0},{""external_patient_id"":""3325494"",""date"":""2026-10-28"",""idh_probability"":0.3033544235027279,""idh_prediction"":0.0,""recommendation"":""增加血壓測量頻率，密切觀察"",""dw_prediction"":37.6,""ut_prediction"":0.0}]}";
        // 實作將預測結果儲存到資料庫的邏輯
        // 例如使用 Dapper 執行 INSERT 語句
        List<HopeCareAIPredictions> lstAIPredictions = new List<HopeCareAIPredictions>();
        try
        {

            var resultData = JsonSerializer.Deserialize<PredictionResponse>(data);

            if (resultData != null)
            {
                foreach (var item in resultData.Anemia)
                {
                    Log.Information($"- model: Anemia");
                    Log.Information($"  predict: {item.AnemiaPrediction}");
                    Log.Information($"  probability: {item.AnemiaProbability}");
                    //Log.Information($"  threshold: {threshold?.ToString() ?? "null"}");

                    var aiPrediction = new HopeCareAIPredictions()
                    {
                        Patientuuid = item.ExternalPatientId,
                        Category = "Anemia",
                        Result = item.AnemiaProbability.ToString(),
                        Probability = item.AnemiaPrediction.ToString()
                        //Threshold = threshold?.ToString()
                    };

                    lstAIPredictions.Add(aiPrediction);
                }

                foreach (var item in resultData.Idh)
                {
                    Log.Information($"- model: Idh");
                    Log.Information($"  predict: {item.IdhPrediction}");
                    Log.Information($"  probability: {item.IdhProbability}");
                    //Log.Information($"  threshold: {threshold?.ToString() ?? "null"}");

                    var aiPrediction = new HopeCareAIPredictions()
                    {
                        Patientuuid = item.ExternalPatientId,
                        Category = "Idh",
                        Result = item.IdhProbability.ToString(),
                        Probability = item.IdhPrediction.ToString()
                        //Threshold = threshold?.ToString()
                    };

                    lstAIPredictions.Add(aiPrediction);
                }

                foreach (var item in resultData.Hgb)
                {
                    Log.Information($"- model: Hgb");
                    Log.Information($"  predict: {item.HgbPrediction}");
                    //Log.Information($"  probability: {item.HgbProbability}");
                    //Log.Information($"  threshold: {threshold?.ToString() ?? "null"}");

                    var aiPrediction = new HopeCareAIPredictions()
                    {
                        Patientuuid = item.ExternalPatientId,
                        Category = "Hgb",
                        Result = item.HgbPrediction.ToString(),
                        Probability = item.HgbPrediction.ToString()
                        //Threshold = threshold?.ToString()
                    };

                    lstAIPredictions.Add(aiPrediction);
                }

                foreach (var item in resultData.Hf)
                {
                    Log.Information($"- model: Hf");
                    Log.Information($"  predict: {item.HfPredictionHospital}");
                    Log.Information($"  probability: {item.HfProbability}");
                    //Log.Information($"  threshold: {threshold?.ToString() ?? "null"}");

                    var aiPrediction = new HopeCareAIPredictions()
                    {
                        Patientuuid = item.ExternalPatientId,
                        Category = "Hf",
                        Result = item.HfPredictionHospital.ToString(),
                        Probability = item.HfProbability.ToString()
                        //Threshold = threshold?.ToString()
                    };

                    lstAIPredictions.Add(aiPrediction);
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
        catch (Exception ex)
        {

            Log.Error(ex, "處理 AI 預測結果時發生錯誤");
            throw;
        }
    }
}
