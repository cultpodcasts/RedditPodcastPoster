using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LabeledMultiDescriptionContainer : BaseContainer
    {
        [JsonProperty("data")]
        public LabeledMultiDescription Data { get; set; }
    }
}
