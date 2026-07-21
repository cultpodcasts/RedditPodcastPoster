using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditNames
    {
        [JsonProperty("names")]
        public List<string> Names { get; set; }
    }
}
