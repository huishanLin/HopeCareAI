using SharedClassLibrary;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace StreamData.Models
{
    public class StreamDto
    {
        [JsonIgnore]

        public string machineid { get; set; }
        [JsonIgnore]
        public int HdId { get; set; }

        [JsonPropertyName("patient_uuid")]
        public string patient_uuid { get; set; }

        [JsonPropertyName("Bed_Name")]
        public string bed_name { get; set; }

        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonPropertyName("TransferTime")]
        public DateTime transfer_time { get; set; }

        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonPropertyName("Start_Time")]
        public DateTime? start_time { get; set; }  // Nullable

        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonPropertyName("End_Time")]
        public DateTime? end_time { get; set; }  // Nullable

        [JsonPropertyName("BPS")]
        public int bps { get; set; }

        [JsonPropertyName("BPD")]
        public int bpd { get; set; }

        [JsonPropertyName("Pulse")]
        public int Pulse { get; set; }

        [JsonPropertyName("HR")]
        public float hr { get; set; }

        [JsonPropertyName("DryWeight")]
        public float dry_weight { get; set; }

        [JsonPropertyName("ArterialBloodFlow")]
        public float arterial_blood_flow { get; set; }

        [JsonPropertyName("ArterialPressure")]
        public float? arterial_pressure { get; set; }

        [JsonPropertyName("BiBagConductivity")]
        public float? bi_bag_conductivity { get; set; }  // Nullable

        [JsonPropertyName("BiBagTemperature")]
        public float? bi_bag_temperature { get; set; }  // Nullable

        [JsonPropertyName("BicarbonateAdjustment")]
        public float? bicarbonate_adjustment { get; set; }  // Nullable

        [JsonPropertyName("BloodFlowRate")]
        public float? blood_flow_rate { get; set; }

        [JsonPropertyName("BloodTemperature")]
        public float? blood_temperature { get; set; }

        [JsonPropertyName("CyclicPressureHoldingTest")]
        public float? cyclic_Pressure_Holding_Test { get; set; }

        [JsonPropertyName("DialysateConductivity")]
        public float? dialysate_conductivity { get; set; }  // Nullable

        [JsonPropertyName("DialysateTemperature")]
        public float? dialysate_temperature { get; set; }  // Nullable

        [JsonPropertyName("DialysisateFlowRate")]
        public float dialysisate_flow_rate { get; set; }

        [JsonPropertyName("HeparinAccumulatedBolusVolume")]
        public float heparin_accumulated_bolus_volume { get; set; }  // Nullable

        [JsonPropertyName("HeparinAccumulatedVolume")]
        public float heparin_accumulated_volume { get; set; }  // Nullable

        [JsonPropertyName("HeparinDeliveryRate")]
        public float heparin_delivery_rate { get; set; }

        [JsonPropertyName("TargetArterialBloodFlow")]
        public float target_arterial_blood_flow { get; set; }

        [JsonPropertyName("TargetSodium")]
        public float? target_sodium { get; set; }

        [JsonPropertyName("TargetUF")]
        public float target_uf { get; set; }

        [JsonPropertyName("TMP")]
        public float tmp { get; set; }

        [JsonPropertyName("TotalUF")]
        public float total_uf { get; set; }

        [JsonPropertyName("UFRate")]
        public float uf_rate { get; set; }

        [JsonPropertyName("VenousPressure")]
        public float venous_pressure { get; set; }
    }
}
