using System;
using System.Collections.Generic;
using System.Text;

namespace Inference.Models
{
    public class InferenceModel
    {
        public Dictionary<string, InferencePredictionModel> Data { get; set; }
    }

    public class InferencePredictionModel
    {
        public InferenceResultModel prediction { get; set; }
    }

    public class InferenceResultModel
    {
        public Dictionary<string, string> model { get; set; }

        public Dictionary<string, float?> predict { get; set; }

        public Dictionary<string, float?> probability { get; set; }

        public Dictionary<string, float?> threshold { get; set; }
    }
}
