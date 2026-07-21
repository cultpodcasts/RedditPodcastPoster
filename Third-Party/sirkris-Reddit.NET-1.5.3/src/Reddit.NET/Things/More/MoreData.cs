using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MoreData : BaseData
    {
        [JsonProperty("children")]
        public List<MoreChild> Children { get; set; }
    }
}
