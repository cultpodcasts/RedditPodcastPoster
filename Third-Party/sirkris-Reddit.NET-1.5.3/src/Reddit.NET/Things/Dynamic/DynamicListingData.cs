using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class DynamicListingData : BaseData
    {
        [JsonProperty("children")]
        public List<DynamicListingChild> Children { get; set; }
    }
}
