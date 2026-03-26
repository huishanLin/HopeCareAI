using System;
using System.Collections.Generic;
using System.Text;

namespace Inference.Models
{
    internal class HopeCareAIPredictions
    {
        public int Id { get; set; }
        public string PatientId { get; set; }
        public string Category { get; set; }
        public string Result { get; set; }
        public string Probability { get; set; }
        public string Threshold { get; set; }
        public DateTime PredictionDate { get; set; }
    }
}
