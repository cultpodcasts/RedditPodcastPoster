using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class DynamicListingChild : BaseContainer
    {
        [JsonProperty("data")]
        public dynamic Data { get; set; }
    }
}
