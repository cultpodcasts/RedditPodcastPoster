using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MultipleResponseData
    {
        [JsonProperty("things")]
        public List<DynamicListingChild> Things { get; set; }
    }
}
