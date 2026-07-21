using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubSearch
    {
        [JsonProperty("subreddits")]
        public List<SubSearchResult> Subreddits { get; set; }
    }
}
