using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace StreamData.Models
{
    public class PredictionResponse
    {
        [JsonPropertyName("anemia")]
        public List<AnemiaItem> Anemia { get; set; } = new List<AnemiaItem>();

        [JsonPropertyName("idh")]
        public List<IdhItem> Idh { get; set; }= new List<IdhItem>();

        [JsonPropertyName("hgb")]
        public List<HgbItem> Hgb { get; set; }= new List<HgbItem>();

        [JsonPropertyName("hf")]
        public List<HfItem> Hf { get; set; } = new List<HfItem>();
    }

    public class AnemiaItem
    {
        [JsonPropertyName("external_patient_id")]
        public string ExternalPatientId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } // 如果需要進行日期運算，可改為 DateTime 屬性

        [JsonPropertyName("anemia_prediction")]
        public decimal AnemiaPrediction { get; set; }

        [JsonPropertyName("anemia_probability")]
        public decimal AnemiaProbability { get; set; }
    }

    public class IdhItem
    {
        [JsonPropertyName("external_patient_id")]
        public string ExternalPatientId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("idh_probability")]
        public decimal IdhProbability { get; set; }

        [JsonPropertyName("idh_prediction")]
        public decimal IdhPrediction { get; set; }

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; }

        [JsonPropertyName("dw_prediction")]
        public decimal DwPrediction { get; set; }

        [JsonPropertyName("ut_prediction")]
        public decimal UtPrediction { get; set; } // 雖然範例為 6，但考量醫療數值，設為 double 較為安全
    }

    public class HgbItem
    {
        [JsonPropertyName("external_patient_id")]
        public string ExternalPatientId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("hgb_prediction")]
        public decimal HgbPrediction { get; set; }
    }

    public class HfItem
    {
        [JsonPropertyName("external_patient_id")]
        public string ExternalPatientId { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("hf_prediction_hospital")]
        public decimal HfPredictionHospital { get; set; }

        [JsonPropertyName("hf_probability")]
        public decimal HfProbability { get; set; }

        [JsonPropertyName("hf_prediction_clinic")]
        public decimal HfPredictionClinic { get; set; }
    }
}
