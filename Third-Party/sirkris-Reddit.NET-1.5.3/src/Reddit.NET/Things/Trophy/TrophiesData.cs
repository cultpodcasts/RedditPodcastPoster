using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class TrophiesData
    {
        [JsonProperty("trophies")]
        public List<AwardContainer> Trophies { get; set; }
    }
}
