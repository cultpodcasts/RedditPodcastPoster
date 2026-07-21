using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class DynamicListingContainer : BaseContainer
    {
        [JsonProperty("data")]
        public DynamicListingData Data { get; set; }
    }
}
