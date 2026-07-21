using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditContainer : BaseContainer
    {
        [JsonProperty("data")]
        public SubredditData Data { get; set; }
    }
}
