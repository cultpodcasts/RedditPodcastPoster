using System;
using Newtonsoft.Json;

namespace Reddit.Things

{
    [Serializable]
    public class OverviewContainer : BaseContainer
    {
        [JsonProperty("data")]
        public OverviewData Data { get; set; }
    }
}
