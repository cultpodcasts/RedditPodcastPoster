using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class DynamicShortListingContainer : BaseContainer
    {
        [JsonProperty("data")]
        public DynamicShortListingData Data { get; set; }
    }
}
