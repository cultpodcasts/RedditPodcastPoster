using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public abstract class BaseResult
    {
        [JsonProperty("errors")]
        public List<List<string>> Errors { get; set; }

        [JsonProperty("ratelimit")]
        public double Ratelimit { get; set; }
    }
}
