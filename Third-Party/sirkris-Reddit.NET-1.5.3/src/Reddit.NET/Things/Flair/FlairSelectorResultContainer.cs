using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class FlairSelectorResultContainer
    {
        [JsonProperty("current")]
        public FlairSelectorResult Current { get; set; }

        [JsonProperty("choices")]
        public List<FlairSelectorResult> Choices { get; set; }
    }
}
