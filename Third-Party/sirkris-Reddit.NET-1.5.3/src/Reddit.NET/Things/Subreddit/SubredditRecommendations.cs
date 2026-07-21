using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditRecommendations
    {
        [JsonProperty("sr_name")]
        public string Name { get; set; }
    }
}
