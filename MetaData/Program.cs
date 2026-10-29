// 1. 初始化 Logger
using Dapper;
using MetaData.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;
using System.Net.Http.Json;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // 設定最小日誌層級
    .WriteTo.Console()    // 輸出到控制台
    .WriteTo.File("logs/meta.txt", rollingInterval: RollingInterval.Day) // 輸出到檔案
    .CreateLogger();

// 建立組態物件，讀取 appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var meta_apiUrl = config.GetSection("MetaApiUrl").Value;
var _connectString_hope_care = config.GetConnectionString("hope_care");
var _labCodeMapping = config
    .GetSection("LabCodeMapping")
    .GetChildren()
    .ToDictionary(x => x.Key, x => x.Value ?? string.Empty); // 讀取 LabCodeMapping

try
{
    using (var sourceConn = new NpgsqlConnection(_connectString_hope_care))
    {
        Log.Information("Connected to the database.");

        var checkDate = DateTime.Today;

        #region SQL Script
        var labCodeClause = string.Join(",\n                            ",
            _labCodeMapping.Select(kvp => $"('{kvp.Key}', e.{kvp.Value})"));

        var selectSql = $@"
                WITH LatestHcdWeight AS (
                    SELECT hdid, dryweight, postweight, preweight,
                           (postweight - coalesce(clothweight2, 0) - coalesce(wchairweight2, 0)) AS calpostweight,
                           (preweight - coalesce(clothweight, 0) - coalesce(wchairweight, 0)) as calpreweight
                    FROM dbo.hcdweight
                    WHERE hddate::date = @hddate
                ), 
                UnpivotedData AS (
                    -- 階段 1: 使用 PostgreSQL 的 LATERAL 進行高效能列轉行，第一時間過濾 NULL
                    SELECT 
                        e.patientid, 
                        e.""date"",
                        v.ExamCode,
                        v.ExamValue
                    FROM dbo.examdata e
                    CROSS JOIN LATERAL (
                        VALUES 
                            {labCodeClause}
                    ) AS v(ExamCode, ExamValue)
                    WHERE v.ExamValue IS NOT null and e.""date"" > CURRENT_TIMESTAMP::date - '1 Year'::INTERVAL
                ),
                tmpExamData AS (
                    -- 階段 2: 針對 (病人, 檢查項目) 進行單一維度的日期遞減排序
                    SELECT 
                        patientid,
                        ExamCode,
                        ExamValue,
                        ""date"", 
                        ROW_NUMBER() OVER (PARTITION BY patientid, ExamCode ORDER BY ""date"" DESC) AS rn
                    FROM UnpivotedData
                ),
                LatestExam AS (
                -- 階段 3: 只取 rn=1 (最新值)，並重新樞紐轉換回直欄
                SELECT 
                    patientid,
                    MAX(CASE WHEN ExamCode = 'hgb' THEN ""date"" END) AS hgb_date,
                    MAX(CASE WHEN ExamCode = 'hgb' THEN ExamValue END) AS lab_hgb,
                    MAX(CASE WHEN ExamCode = 'creat' THEN ExamValue END) AS lab_creat,
                    MAX(CASE WHEN ExamCode = 'hba1c' THEN ExamValue END) AS lab_hba1c,
                    MAX(CASE WHEN ExamCode = 'cl' THEN ExamValue END) AS lab_cl,
                    MAX(CASE WHEN ExamCode = 'ca' THEN ExamValue END) AS lab_ca,
                    MAX(CASE WHEN ExamCode = 'na' THEN ExamValue END) AS lab_na,
                    MAX(CASE WHEN ExamCode = 'k' THEN ExamValue END) AS lab_k,
                    MAX(CASE WHEN ExamCode = 'tsat' THEN ExamValue END) AS lab_tsat,
                    MAX(CASE WHEN ExamCode = 'urr' THEN ExamValue END) AS lab_urr,
                    MAX(CASE WHEN ExamCode = 'alb' THEN ExamValue END) AS lab_alb,
                    MAX(CASE WHEN ExamCode = 'sp_ktv_d' THEN ExamValue END) AS lab_sp_ktv_d,
                    MAX(CASE WHEN ExamCode = 'ktv_g' THEN ExamValue END) AS lab_ktv_g
                FROM tmpExamData
                WHERE rn = 1
                GROUP BY patientid
                ),
                RankedList AS (
                    SELECT id, patientid, sex, chartid, bedno , hddate , systolics, diastolics, pulses ,birthday, ROW_NUMBER() OVER (PARTITION BY patientid ORDER BY hddate DESC) AS rn
                    FROM dbo.hcmhdlist
                    WHERE rcdstatus between '2' and '3' and hddate::date = @hddate
                ),
                LatestPlan AS (
                    SELECT hdid, hdliqk, hdliqna
                    FROM dbo.hcdplanrcd
                    WHERE hddate::date = @hddate
                ),
                 tmpEPI as (
	                SELECT 
		                vepi.patient_id,
		                -- 如果計算數量大於 0，則結果為 1，否則為 0
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 1) > 0 THEN 1 ELSE 0 END AS hf,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 2) > 0 THEN 1 ELSE 0 END AS mi,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 4) > 0 THEN 1 ELSE 0 END AS cva,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 8) > 0 THEN 1 ELSE 0 END AS cancer,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 19) > 0 THEN 1 ELSE 0 END AS hypertension,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 20) > 0 THEN 1 ELSE 0 END AS cad,
		                CASE WHEN COUNT(*) FILTER (WHERE vepi.epi_code = 22) > 0 THEN 1 ELSE 0 END AS dm
	                FROM (
			                SELECT 
			                    e.id AS epi_id,
			                    e.patientid AS patient_id,
			                    e.""date"" AS eval_date,
			                    e.desc1 AS desc_heart,
			                    e.desc2 AS desc_other,
			                    s.single_epi::int AS epi_code
			                FROM dbo.epi e
			                LEFT JOIN LATERAL (
			                    SELECT elem AS single_epi
			                    FROM unnest(string_to_array(e.epis, '-')) AS elem
			                    WHERE elem ~ '^[0-9]+$' -- 嚴格防禦：確保僅純數字才進行 int 轉型，防止髒資料爆表
			                ) s ON true
			                WHERE e.epis IS NOT NULL AND e.epis <> '') AS vepi
	                GROUP BY vepi.patient_id)
                SELECT 
	                p.id,
	                --CONCAT('2HDU',p.chartid) AS chartid,
                    p.chartid AS chartid,   
	                CURRENT_TIMESTAMP AS timestamp,
	                DATE_PART('year', AGE(list.birthday)) AS age,
	                list.sex AS gender,
	                h.calpreweight AS pre_weight,
	                coalesce(h.calpostweight, h.dryweight) AS post_weight,
	                DATE_PART('year', AGE(p.firstdate)) AS dialysis_vintage,
	                CURRENT_DATE - ex.hgb_date::date AS days_since_hgb,
	                list.systolics AS bps,
	                list.diastolics AS bpd,
	                list.pulses AS hr,
	                coalesce(tepi.cancer, 0) AS cancer,
	                coalesce(tepi.cva, 0) AS cva,
	                coalesce(tepi.cad, 0) AS cad,
	                coalesce(tepi.dm, 0) AS dm,
	                coalesce(tepi.hf, 0) AS hf,
	                coalesce(tepi.hypertension, 0) AS hypertension,
	                coalesce(tepi.mi, 0) AS mi,
	                --coalesce(tepi.paod, 0) as paod,
	                null AS lvef,
	                ex.lab_ca,
	                ex.lab_ktv_g,
	                ex.lab_tsat,
	                ex.lab_urr,
	                ex.lab_alb,
	                ex.lab_creat,
	                ex.lab_hgb,
	                ex.lab_k,
	                ex.lab_na,
	                ex.lab_cl,
	                ex.lab_hba1c,
	                --ex.lab_ktv_lowrie,
	                ex.lab_sp_ktv_d
                FROM
	                RankedList AS list
	                INNER JOIN dbo.patient p  ON list.patientid = p.id
                    LEFT JOIN LatestExam AS ex ON list.patientid = ex.patientid
                    INNER JOIN LatestHcdWeight AS h ON list.id = h.hdid
                    INNER JOIN LatestPlan AS plan ON list.id = plan.hdid
                    INNER JOIN dbo.start AS a ON list.patientid = a.patientid
                    LEFT JOIN tmpEPI tepi ON p.id = tepi.patient_id
                WHERE list.rn = 1;";
        #endregion

        await sourceConn.OpenAsync();
        var result = await sourceConn.QueryAsync<MetaDto>(selectSql, new { hddate = checkDate });

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
            var response = await httpClient.PostAsJsonAsync(meta_apiUrl, result);

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