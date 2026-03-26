// See https://aka.ms/new-console-template for more information

using Dapper;
using Inference.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;
using System.Net.Http.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // 設定最小日誌層級
    .WriteTo.Console()    // 輸出到控制台
    .WriteTo.File("logs/inference.txt", rollingInterval: RollingInterval.Day) // 輸出到檔案
    .CreateLogger();

// 建立組態物件，讀取 appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var inference_apiUrl = config.GetSection("InferenceApiUrl").Value;
var _connectString_hope_care = config.GetConnectionString("hope_care");

try
{
    using (var sourceConn = new NpgsqlConnection(_connectString_hope_care))
    {
        Log.Information("Connected to the database.");
        var selecthcmhdlistSQL = @"
        SELECT patientid
        FROM dbo.hcmhdlist 
        WHERE rcdstatus between '2' and '4' and hddate::date = @hddate";

        await sourceConn.OpenAsync();
        var result = await sourceConn.QueryAsync<string>(selecthcmhdlistSQL, new { hddate = DateTime.Today.AddDays(-1) });

        if (result.Count() == 0)
        {
            Log.Information("無透析中的病人資料");
            return;
        }

        try
        {
            // 將 batch 以 JSON 格式 POST 到指定 URL
            using var httpClient = new HttpClient();
            //var jsonString = JsonSerializer.Serialize(selectSql); // 👈 序列化後就能看到內容
            //logger.Info("Request Body:\n" + jsonString);
            var response = await httpClient.PostAsJsonAsync(inference_apiUrl, result);

            if (response.IsSuccessStatusCode)
            {
                //logger.Info($"本批次實際處理 {batch.Count} 筆，POST 成功");
                //Log.Information($"POST 成功，共處理 {result.AsList().Count} 筆資料");

                //var read = await response.Content.ReadAsStreamAsync();
                //var read2 = await response.Content.ReadAsStringAsync();

                List<HopeCareAIPredictions> lstAIPredictions = new List<HopeCareAIPredictions>();
                var resultData = await response.Content.ReadFromJsonAsync<List<Dictionary<string, InferencePredictionModel>>>();

                if (resultData != null)
                {
                    foreach (var dict in resultData)
                    {
                        foreach (var entry in dict)
                        {
                            
                             var patientId = entry.Key; // 這裡拿到 "6297"
                            var content = entry.Value.prediction;

                            Log.Information($"處理患者 ID: {patientId}");

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
                                    PatientId = patientId,
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
                        var insertSQL = @"Insert into dbo.aipredictions
                                        (patientid, category, result, probability, threshold, predictiondate)
                                        values
                                        (@PatientId, @Category, @Result, @Probability, @Threshold, CURRENT_TIMESTAMP)";
                       var executeCount =  await sourceConn.ExecuteAsync(insertSQL, lstAIPredictions);
                        Log.Information($"已新增 {executeCount} 筆 AI 預測結果至資料庫。");
                    }
                }
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
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while connecting to the database.");
}
finally
{
    Log.CloseAndFlush();
}
