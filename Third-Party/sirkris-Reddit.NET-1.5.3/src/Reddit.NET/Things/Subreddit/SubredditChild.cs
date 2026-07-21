using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditChild : BaseContainer
    {
        [JsonProperty("data")]
        public Subreddit Data { get; set; }
    }
}
