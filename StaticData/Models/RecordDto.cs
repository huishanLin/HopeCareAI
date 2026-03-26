using SharedClassLibrary;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace StaticData.Models
{
    public class RecordDto
    {
        [JsonPropertyName("patient_uuid")]
        public string patient_uuid { get; set; } = "";

        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonPropertyName("Timestamp")]
        public DateTime timestamp { get; set; }

        //[JsonPropertyName("Medical_ID")]
        //public string medical_id { get; set; } = "";

        [JsonPropertyName("Bed_Name")]
        public string bed_name { get; set; } = "";

        [JsonPropertyName("Gender")]
        public int gender { get; set; }

        [JsonPropertyName("Age")]
        public int age { get; set; }

        [JsonPropertyName("Height")]
        public float height { get; set; }

        [JsonPropertyName("DryWeight")]
        public float dry_weight { get; set; }

        [JsonPropertyName("WeightPostDialysis")]
        public float weight_post_dialysis { get; set; }

        [JsonPropertyName("WeightPreDialysis")]
        public float weight_pre_dialysis { get; set; }

        [JsonPropertyName("DeltaWeight")]
        public float delta_weight { get; set; }

        [JsonPropertyName("BPS")]
        public int bps { get; set; }

        [JsonPropertyName("BPD")]
        public int bpd { get; set; }

        [JsonPropertyName("HR")]
        public int hr { get; set; }

        [JsonPropertyName("CREAT")]
        public float creat { get; set; }

        [JsonPropertyName("NaConcentration")]
        public float na_concentration { get; set; }

        [JsonPropertyName("KConcentration")]
        public float k_concentration { get; set; }

        [JsonPropertyName("ClConcentration")]
        public float cl_concentration { get; set; }

        [JsonPropertyName("HGB")]
        public float hgb { get; set; }

        [JsonPropertyName("HBA1C")]
        public float hba1c { get; set; }

        //[JsonPropertyName("HeparinOriginal")]
        //public int heparin_original { get; set; }

        [JsonPropertyName("X_ray_Image")]
        public string x_ray_image { get; set; } = "";

        [JsonPropertyName("ARBs_C09CA")]
        public int arbs_c09ca { get; set; } = 0;

        [JsonPropertyName("BetaBlocker_C07AG")]
        public int betablocker_c07ag { get; set;} = 0;

        [JsonPropertyName("CalciumChannelBlocker_C08CA")]
        public int calciumchannelblocker_c08ca { get; set; } = 0;

        [JsonPropertyName("Diuretics_C03CA")]
        public int diuretics_c03ca { get; set; } = 0;

        [JsonPropertyName("Insulin_A10AB")]
        public int insulin_a10ab { get; set; } = 0;
        [JsonPropertyName("Nitrates_C01DA")]
        public int nitrates_c01da { get; set; } = 0;

        [JsonPropertyName("CA")]
        public float ca { get; set; }


    }
}
