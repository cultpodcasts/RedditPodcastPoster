using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Reddit.Things
{
    [Serializable]
    public class MixedListingChild : BaseContainer
    {
        [JsonProperty("data")]
        public JObject Data { get; set; }
    }
}
