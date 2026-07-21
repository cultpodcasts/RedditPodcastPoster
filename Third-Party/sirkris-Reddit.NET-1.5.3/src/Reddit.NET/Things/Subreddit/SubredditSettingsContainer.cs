using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditSettingsContainer : BaseContainer
    {
        [JsonProperty("data")]
        public SubredditSettings Data { get; set; }
    }
}
