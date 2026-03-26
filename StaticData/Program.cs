// See https://aka.ms/new-console-template for more information
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using StaticData.Models;
using System.Net.Http.Json;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

// 1. 初始化 Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // 設定最小日誌層級
    .WriteTo.Console()    // 輸出到控制台
    .WriteTo.File("logs/static.txt", rollingInterval: RollingInterval.Day) // 輸出到檔案
    .CreateLogger();

// 建立組態物件，讀取 appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var static_apiUrl = config.GetSection("StaticApiUrl").Value;
var _connectString_hope_care = config.GetConnectionString("hope_care");

try
{
    using (var sourceConn = new NpgsqlConnection(_connectString_hope_care))
    {
        Log.Information("Connected to the database.");

        #region SQL Script
        var selectSql = @"
                WITH LatestHcdWeight AS (
                    SELECT hdid, dryweight , postweight , preweight
                    FROM dbo.hcdweight
                    WHERE hddate::date = @hddate
                ), 
                LatestExam AS (
                    SELECT 
					    patientid,
					    MAX(latest_exam18) AS nexam18,
					    MAX(latest_exam40) AS nexam40,
					    MAX(latest_exam66) AS nexam66,
					    MAX(latest_exam48) AS nexam48,
					    MAX(latest_exam50) AS nexam50,
                        MAX(latest_exam44) AS nexam44,
                        MAX(latest_exam46) AS nexam46
					FROM (
					    SELECT 
					        patientid,
					        FIRST_VALUE(nexam18) OVER (PARTITION BY patientid ORDER BY (nexam18 IS NULL), ""date"" DESC) AS latest_exam18,
					        FIRST_VALUE(nexam40) OVER (PARTITION BY patientid ORDER BY (nexam40 IS NULL), ""date"" DESC) AS latest_exam40,
					        FIRST_VALUE(nexam66) OVER (PARTITION BY patientid ORDER BY (nexam66 IS NULL), ""date"" DESC) AS latest_exam66,
					        FIRST_VALUE(nexam48) OVER (PARTITION BY patientid ORDER BY (nexam48 IS NULL), ""date"" DESC) AS latest_exam48,
					        FIRST_VALUE(nexam50) OVER (PARTITION BY patientid ORDER BY (nexam50 IS NULL), ""date"" DESC) AS latest_exam50,
                            FIRST_VALUE(nexam44) OVER (PARTITION BY patientid ORDER BY (nexam44 IS NULL), ""date"" DESC) AS latest_exam44,
                            FIRST_VALUE(nexam46) OVER (PARTITION BY patientid ORDER BY (nexam46 IS NULL), ""date"" DESC) AS latest_exam46
					    FROM dbo.examdata
					) sub
					GROUP BY patientid
                ),
                RankedList AS (
                    SELECT id, patientid, sex, chartid, bedno , hddate , systolics, diastolics, pulses ,birthday, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
                    FROM dbo.hcmhdlist
                    WHERE rcdstatus between '2' and '4' and hddate::date = @hddate
                ),
                LatestPlan AS (
                    SELECT hdid, hdliqk, hdliqna
                    FROM dbo.hcdplanrcd
                    WHERE hddate::date = @hddate
                ),
                UseMed as (
                	SELECT 
					    h.patientid,
					    -- 如果計算數量大於 0，則結果為 1，否則為 0
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'C09CA') > 0 THEN 1 ELSE 0 END AS has_C09CA,
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'C07AG') > 0 THEN 1 ELSE 0 END AS has_C07AG,
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'C08CA') > 0 THEN 1 ELSE 0 END AS has_C08CA,
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'C03CA') > 0 THEN 1 ELSE 0 END AS has_C03CA,
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'A10AB') > 0 THEN 1 ELSE 0 END AS has_A10AB,
					    CASE WHEN COUNT(*) FILTER (WHERE p.atc_code = 'C01DA') > 0 THEN 1 ELSE 0 END AS has_C01DA
					FROM 
					    dbo.hcddrug h
					JOIN 
					    dbo.pricecode p ON h.feecode = p.feecode
					WHERE 
					    h.onflag = '1' and p.atc_code in ('C09CA','C07AG','C08CA','C03CA','A10AB','C01DA')
					GROUP BY 
					    h.patientid
                )
                SELECT 
                    list.chartid AS patient_uuid,
                    list.hddate,
                    CURRENT_TIMESTAMP AS timestamp,
                    list.chartid AS medical_id, -- 非必填欄位
                    list.bedno AS bed_name, -- 非必填欄位
                    CASE 
                        WHEN list.sex::integer = 1 THEN 0
                        WHEN list.sex::integer = 2 THEN 1
                        ELSE NULL
                    END AS gender,
                    DATE_PART('year', AGE(list.birthday)) AS age,
                    coalesce(a.sexamheight, 177)/100.0 AS height,
                    h.dryweight AS dry_weight,
                    h.postweight AS weight_post_dialysis,
                    h.preweight AS weight_pre_dialysis,
                    (h.preweight - h.postweight) AS delta_weight,
                    list.systolics AS bps,
                    list.diastolics AS bpd,
                    list.pulses AS hr,
                    ex.nexam40 AS creat,
                    ex.nexam44 AS na_concentration,
                    ex.nexam46 AS k_concentration,
                    ex.nexam48 AS cl_concentration,
                    ex.nexam18 AS hgb,
                    ex.nexam66 AS hba1c,
                    ex.nexam50 AS ca,
                    'N/A' AS x_ray_image, -- 非必填欄位
                    coalesce(med.has_C09CA, 0) as arbs_c09ca,
                    coalesce(med.has_C07AG, 0) as betablocker_c07ag,
                    coalesce(med.has_C08CA, 0) as calciumchannelblocker_c08ca,
                    coalesce(med.has_C03CA, 0) as diuretics_c03ca,
                    coalesce(med.has_A10AB, 0) as insulin_a10ab,
                    coalesce(med.has_C01DA, 0) as nitrates_c01da
                FROM RankedList AS list
                Left JOIN LatestExam AS ex ON list.patientid = ex.patientid
                INNER JOIN LatestHcdWeight AS h ON list.id = h.hdid
                INNER JOIN LatestPlan AS p ON list.id = p.hdid
                INNER JOIN dbo.start AS a ON list.patientid = a.patientid
                left join UseMed as med on med.patientid = list.patientid
                WHERE list.rn = 1;";
        #endregion

        await sourceConn.OpenAsync();
        var result = await sourceConn.QueryAsync<RecordDto>(selectSql, new { hddate = DateTime.Today });

        if (result.Count() == 0)
        {
            Log.Information("無資料需要上傳");
            return;
        }

        try
        {
            // 將 batch 以 JSON 格式 POST 到指定 URL
            using var httpClient = new HttpClient();
            var jsonString = JsonSerializer.Serialize(result); // 👈 序列化後就能看到內容
            Log.Information("Request Body:\n" + jsonString);
            var response = await httpClient.PostAsJsonAsync(static_apiUrl, result);

            if (response.IsSuccessStatusCode)
            {
                var resultData = await response.Content.ReadAsStringAsync();
                //logger.Info($"本批次實際處理 {batch.Count} 筆，POST 成功");
                Log.Information($"POST 成功，共處理 {result.AsList().Count} 筆資料。回應內容:{resultData}");
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
    Log.Error(ex, "發生未預期的錯誤");
}
finally
{
    Log.CloseAndFlush();
}

