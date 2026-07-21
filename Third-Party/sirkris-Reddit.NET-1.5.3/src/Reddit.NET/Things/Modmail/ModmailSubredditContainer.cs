using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ModmailSubredditContainer
    {
        [JsonProperty("subreddits")]
        public Dictionary<string, ModmailSubreddit> Subreddits { get; set; }
    }
}
