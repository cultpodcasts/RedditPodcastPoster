using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LiveUpdateData : BaseData
    {
        [JsonProperty("children")]
        public List<LiveUpdateChild> Children { get; set; }
    }
}
