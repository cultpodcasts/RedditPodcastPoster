using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class DynamicShortListingData : BaseData
    {
        [JsonProperty("children")]
        public List<dynamic> Children { get; set; }
    }
}
