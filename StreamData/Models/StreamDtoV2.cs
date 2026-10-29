using SharedClassLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace StreamData.Models
{
    public class StreamDtoV2 : IStreamData
    {
        public string MachineIdentifier => machine_model;

        /// <summary>
        /// 病患識別碼 / External Patient ID | string \| null | - | 對應院內系統之病患唯一識別碼 |
        /// 來源 : hcmhdlist.chartid
        /// </summary>
        [JsonPropertyName("external_patient_id")]
        public string  chartid { get; set; }

        /// <summary>
        /// 日期 / Date | date \| null | - | 資料所屬日期 |
        /// hcthdgdata.hddate
        /// </summary>
        [JsonPropertyName("date")]
        public DateOnly transfer_time { get; set; }

        /// <summary>
        /// 床位名稱 / Bed Name | string \| null | - | 透析機台所在之病床/床位名稱 |
        /// 來源 : hcmhdlist.bedno
        /// </summary>
        public string bed_name { get; set; }

        /// <summary>
        /// 機台型號 / Machine Model | string \| null | - | 透析機型號 |
        /// 來源 : hcthdgdata.machineid
        /// </summary>
        public string machine_model { get; set; }

        /// <summary>
        /// 血流速 / Blood Flow Rate | number \| null | mL/min | 體外循環血液幫浦實際運轉之血流速度 |
        /// 來源 : hcthdgdata.bloodflow
        /// </summary>
        public float? blood_flow_rate { get; set; }

        /// <summary>
        /// 超濾速率 / Ultrafiltration Rate | number \| null | L/hr | 單位時間內自病患體內移除水分之速率 |
        /// 來源 : hcthdgdata.ufrate
        /// </summary>
        public float? uf_rate { get; set; }

        /// <summary>
        /// 動脈壓 / Arterial Pressure | number \| null | mmHg | 體外循環動脈端（引血端）管路壓力 |
        /// 來源 : hcdplanrcd.bloodflow
        /// </summary>
        public float? arterial_pressure { get; set; }

        /// <summary>
        /// 血流量 / Blood Flow Volume | number \| null |  L | 本次治療累計流經體外循環之血液總量 |
        /// 來源 : hcthdgdata.AccBloodVol
        /// </summary>
        public float? blood_flow_volume { get; set; }

        /// <summary>
        /// 收縮壓 / Systolic Blood Pressure | number \| null | mmHg | 治療中量測之收縮壓 |
        /// 來源 : hcthdgdata.systolic 如果沒有則抓 hcdrecord.systolics
        /// </summary>
        public float? blood_pressure_systolic { get; set; }

        /// <summary>
        /// 舒張壓 / Diastolic Blood Pressure | number \| null | mmHg | 治療中量測之舒張壓 |
        /// 來源 : hcthdgdata.diastolic 如果沒有則抓 hcdrecord.diastolics
        /// </summary>
        public float? blood_pressure_diastolic { get; set; }

        /// <summary>
        /// 透析液流速 / Dialysate Flow Rate | number \| null | mL/min | 透析液實際流動速度 |
        /// 來源 : hcthdgdata.liqflow
        /// </summary>
        public float? dialysate_flow_rate { get; set; }

        /// <summary>
        /// 脈搏（心跳）速率 / Pulse Rate | number \| null | bpm（次/分） | 治療中量測之脈搏速率 |
        /// 來源 : hcthdgdata.pulse 如果沒有則抓 hcdrecord.pulse
        /// </summary>
        public float? pulse_rate { get; set; }

        /// <summary>
        /// 跨膜壓 / Transmembrane Pressure (TMP) | number \| null | mmHg | 透析膜（濾器）兩側血液與透析液之壓力差，用以反映超濾狀況 |
        /// 來源 : hcthdgdata.tmp
        /// </summary>
        public float? transmembrane_pressure { get; set; }

        /// <summary>
        /// 超濾量 / Ultrafiltration Volume | number \| null | L | 本次治療累計已移除之水分總量 |
        /// 來源 : hcthdgdata.ufvolume
        /// </summary>
        public float? uf_volume { get; set; }

        /// <summary>
        /// 靜脈壓 / Venous Pressure | number \| null | mmHg | 體外循環靜脈端（回血端）管路壓力 |
        /// 來源 : hcthdgdata.vp
        /// </summary>
        public float? venous_pressure { get; set; }

        /// <summary>
        /// 透析液導電度 / Dialysate Conductivity | number \| null | mS/cm | 反映透析液電解質（主要為鈉離子）濃度 |
        /// 來源 : hcthdgdata.liqcond
        /// </summary>
        public float? dialysate_conductivity { get; set; }

        /// <summary>
        /// 透析液溫度 / Dialysate Temperature | number \| null | °C | 透析液實際溫度 |
        /// 來源 : hcthdgdata.liqtemp
        /// </summary>
        public float? dialysate_temperature { get; set; }

        /// <summary>
        /// 肝素累積注射量 / Heparin Accumulated Volume | number \| null | mL | 本次治療累計已輸注之抗凝劑（肝素）量 |
        /// 來源 : hcthdgdata.anticglqty
        /// </summary>
        public float? heparin_accumulated_volume { get; set; }

        /// <summary>
        /// 肝素給藥速率 / Heparin Delivery Rate | number \| null | mL/hr | 抗凝劑幫浦目前輸注速度 |
        /// 來源 : hcthdgdata.anticglflow
        /// </summary>
        public float? heparin_delivery_rate { get; set; }

        /// <summary>
        /// 目標鈉濃度 / Target Sodium | number \| null | mEq/L（或 mmol/L） | 透析液鈉離子設定目標值（常用於鈉建模 sodium profiling） |
        /// 來源 : hcdplanrcd.hdliqna
        /// </summary>
        public float? target_sodium { get; set; }

        /// <summary>
        /// 目標除水量 / Target UF (UF Goal) | number \| null |  L | 本次治療設定欲移除之水分總量 |
        /// 來源 : hcthdgdata.ufgoal
        /// </summary>
        public float? target_uf { get; set; }

        /// <summary>
        /// 除水時間 / UF Time | number \| null | min | 設定之除水總時間，通常等同治療時間 |
        /// 來源 : hcthdgdata.uftime
        /// </summary>
        public float? uf_time { get; set; }

        /// <summary>
        /// 動脈血流設定值 / Arterial Blood Flow (Set) | number \| null | mL/min | 操作者設定之血流速目標值 |
        /// 來源 : hcdplanrcd.bloodflow
        /// </summary>
        public float? arterial_blood_flow_set { get; set; }

        /// <summary>
        /// 透析液溫度設定值 / Dialysate Temperature (Set) | number \| null | °C | 操作者設定之透析液溫度目標值 |
        /// 來源 : hcdplanrcd.liqtemp
        /// </summary>
        public float? dialysate_temperature_set { get; set; }

        /// <summary>
        /// 透析液流速設定值 / Dialysate Flow Rate (Set) | number \| null | mL/min | 操作者設定之透析液流速目標值 |
        /// 來源 : hcdplanrcd.liqflow
        /// </summary>
        public float? dialysate_flow_rate_set { get; set; }
    }
}
