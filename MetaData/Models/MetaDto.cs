using SharedClassLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MetaData.Models
{
    public class MetaDto
    {
        /// <summary>
        /// 病患識別碼 / External Patient ID | string \| null | - | 對應院內系統之病患唯一識別碼 |
        /// 來源 : patient.chartid 
        /// </summary>
        [JsonPropertyName("external_patient_id")]
        public string chartid { get; set; }

        /// <summary>
        /// 資料時間戳記 / Client Timestamp | date-time \| null | - | 該筆 meta 資料產生（回報）的時間 |
        /// 來源 : 當下的時間
        /// </summary>
        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonPropertyName("client_timestamp")]
        public DateTime timestamp { get; set; }

        /// <summary>
        /// 年齡 / Age | integer \| null | 歲 (years) | 病患年齡 |
        /// 來源 : hcmhdlist.birthday 依照當下透析日期計算年齡
        /// </summary>
        public int age { get; set; }
        /// <summary>
        /// 性別 / Gender | integer \| null | 代碼 | 性別代碼，1=男/2=女
        /// 來源 : hcmhdlist.gender
        /// </summary>
        public int gender { get; set; }

        /// <summary>
        /// 透析前體重 / Pre-dialysis Weight | number \| null | kg | 治療開始前量測之體重 ；若是首次透析病人則填dry_weight |
        /// 來源 : hcdweight.preweight 計算而來
        /// </summary>
        public decimal pre_weight { get; set; }

        /// <summary>
        /// 透析後體重 / Post-dialysis Weight | number \| null | kg | 治療結束後量測之體重；若是首次透析病人則填dry_weight |
        /// 來源 : hcdweight.postweight 計算而來
        /// </summary>
        public decimal post_weight { get; set; }

        /// <summary>
        /// 透析年資 / Dialysis Vintage | integer \| null | 年(year) | 病患開始接受透析治療至今的時間長度 ；若無該項則依以下方式計算：1.依第一次洗腎日期進行運算；2.若無資料則填入 null|
        /// 來源 : patient.firstdate 計算而來
        /// </summary>
        public int dialysis_vintage { get; set; }

        /// <summary>
        /// 距上次血紅素檢驗天數 / Days Since Last Hgb Test | integer \| null | 天 (days) | 距離上一次血色素（Hgb）檢驗的天數 ；若無該項則依以下方式計算：1.依lab_hgb_date-當下透析日期進行轉換運算2.若無HGB檢驗日期則填null|
        /// 來源 : examdata.date 有 hgb 有值的時間，計算而來
        /// </summary>
        public int days_since_hgb { get; set; }

        /// <summary>
        /// 透析前收縮壓 / Pre-dialysis Systolic BP | number \| null | mmHg | 治療前量測之收縮壓，可抓第一筆透析中收縮壓 |
        /// 來源 : hcmhdlist.systolics 計算而來
        /// </summary>
        [JsonPropertyName("pre_blood_pressure_systolic")]
        public int bps { get; set; }

        /// <summary>
        /// 透析前舒張壓 / Pre-dialysis Diastolic BP | number \| null | mmHg | 治療前量測之舒張壓，可抓第一筆透析中舒張壓 |
        /// 來源 : hcmhdlist.diastolics
        /// </summary>
        [JsonPropertyName("pre_blood_pressure_diastolic")]
        public int bpd { get; set; }

        /// <summary>
        /// 透析前脈搏速率 / Pre-dialysis Pulse Rate | number \| null | bpm（次/分） | 治療前量測之脈搏速率，可抓第一筆透析中脈搏速率|
        /// 來源 : hcmhdlist.pulses
        /// </summary>
        [JsonPropertyName("pre_pulse_rate")]
        public int hr { get; set; }

        /// <summary>
        /// 癌症病史 / Cancer History | integer \| null | 布林代碼 (0=無, 1=有) | 是否有癌症病史 |
        /// 來源 : epi.epis 有設定 8
        /// </summary>
        public int cancer { get; set; }

        /// <summary>
        /// 腦血管病史 / Cerebrovascular Accident (CVA) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有腦中風／腦血管疾病病史 |
        /// 來源 : epi.epis 有設定 4
        /// </summary>
        public int cva { get; set; }

        /// <summary>
        /// 冠狀動脈疾病病史 / Coronary Artery Disease (CAD) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有冠狀動脈（心臟血管）疾病病史 |
        /// 來源 : epi.epis 有設定 20
        /// </summary>
        public int cad { get; set; }

        /// <summary>
        /// 糖尿病病史 / Diabetes Mellitus (DM) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有糖尿病病史 |
        /// 來源 : epi.epis 有設定 22
        /// </summary>
        public int dm { get; set; }

        /// <summary>
        /// 心臟衰竭病史 / Heart Failure (HF) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有心臟衰竭病史 |
        /// 來源 : epi.epis 有設定 1
        /// </summary>
        public int hf { get; set; }

        /// <summary>
        /// 高血壓病史 / Hypertension | integer \| null | 布林代碼 (0=無, 1=有) | 是否有高血壓病史 |
        /// 來源 : epi.epis 有設定 19
        /// </summary>
        public int hypertension { get; set; }

        /// <summary>
        /// 心肌梗塞病史 / Myocardial Infarction (MI) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有心肌梗塞病史 |
        /// 來源 : epi.epis 有設定 2
        /// </summary>
        public int mi { get; set; }

        /// <summary>
        /// 周邊動脈阻塞疾病病史 / Peripheral Arterial Occlusive Disease (PAOD) | integer \| null | 布林代碼 (0=無, 1=有) | 是否有周邊動脈阻塞性疾病病史 |
        /// 來源 : 無
        /// </summary>
        public int paod { get; set; }

        /// <summary>
        /// 左心室射出分率 / Left Ventricular Ejection Fraction (LVEF) | number \| null | % | 反映心臟收縮功能之指標 |
        /// 來源 : 無
        /// </summary>
        public int lvef { get; set; }

        /// <summary>
        /// 血鈣 / Calcium (Ca) | number \| null | mg/dL | 血液中鈣離子濃度檢驗值 |
        /// 來源 : examdata.nexam50
        /// </summary>
        public decimal lab_ca { get; set; }

        /// <summary>
        /// 尿素清除率Kt/V(G) / Kt/V (Gotch) | number \| null | 無單位（比值） | 透析劑量／透析效能指標，Gotch 公式計算值 |
        /// 來源 : examdata.nexam57
        /// </summary>
        public decimal lab_ktv_g { get; set; }

        /// <summary>
        /// 運鐵蛋白飽和度 / Transferrin Saturation (TSAT) | number \| null | % | 反映體內鐵儲存與利用狀態 |
        /// 來源 : examdata.nexam25
        /// </summary>
        public decimal lab_tsat { get; set; }

        /// <summary>
        /// 尿素氮下降率 / Urea Reduction Ratio (URR) | number \| null | % | 透析前後尿素氮下降比例，反映透析效能 |
        /// 來源 : examdata.nexam56
        /// </summary>
        public decimal lab_urr { get; set; }

        /// <summary>
        /// 白蛋白 / Albumin (Alb) | number \| null | g/dL | 血液白蛋白濃度，反映營養狀態 |
        /// 來源 : examdata.nexam02
        /// </summary>
        public decimal lab_alb { get; set; }

        /// <summary>
        /// 肌酸酐 / Creatinine (Creat) | number \| null | mg/dL | 血液肌酸酐濃度，反映腎功能與肌肉量 |
        /// 來源 : examdata.nexam40
        /// </summary>
        public decimal lab_creat { get; set; }

        /// <summary>
        /// 血紅素 / Hemoglobin (Hgb) | number \| null | g/dL | 血液血紅素濃度，反映貧血程度 |
        /// 來源 : examdata.nexam18
        /// </summary>
        public decimal lab_hgb { get; set; }

        /// <summary>
        /// 血鉀 / Potassium (K) | number \| null | mEq/L | 血液中鉀離子濃度檢驗值 |
        /// 來源 : examdata.nexam46
        /// </summary>
        public decimal lab_k { get; set; }

        /// <summary>
        /// 血鈉 / Sodium (Na) | number \| null | mEq/L | 血液中鈉離子濃度檢驗值 |
        /// 來源 : examdata.nexam44
        /// </summary>
        public decimal lab_na { get; set; }

        /// <summary>
        /// 血氯 / Chloride (Cl) | number \| null | mEq/L | 血液中氯離子濃度檢驗值 |
        /// 來源 : examdata.nexam48
        /// </summary>
        public decimal lab_cl { get; set; }

        /// <summary>
        /// 糖化血色素 / Hemoglobin A1c (HbA1c) | number \| null | % | 反映近 2-3 個月平均血糖控制狀況 |
        /// 來源 : examdata.nexam67
        /// </summary>
        public decimal lab_hba1c { get; set; }

        /// <summary>
        /// Kt/V（Lowrie 公式） / Kt/V (Lowrie) | number \| null | 無單位（比值） | 透析劑量指標，Lowrie 公式計算值 |
        /// 來源 : 無
        /// </summary>
        public decimal lab_ktv_lowrie { get; set; }

        /// <summary>
        /// 每日單池 Kt/V(d) / Single-pool Kt/V (daily) | number \| null | 無單位（比值） | 單池模型計算之每日透析劑量指標 |
        /// 來源 : examdata.nexam60
        /// </summary>
        public decimal lab_sp_ktv_d { get; set; }
    }
}
