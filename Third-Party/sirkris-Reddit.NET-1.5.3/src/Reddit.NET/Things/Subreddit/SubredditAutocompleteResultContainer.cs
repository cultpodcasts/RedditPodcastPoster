using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditAutocompleteResultContainer
    {
        [JsonProperty("subreddits")]
        public List<SubredditAutocompleteResult> Subreddits { get; set; }
    }
}
