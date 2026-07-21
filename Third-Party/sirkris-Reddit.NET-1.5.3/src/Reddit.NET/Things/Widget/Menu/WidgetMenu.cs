using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetMenu : BaseContainer
    {
        [JsonProperty("data")]
        public List<WidgetMenuDataLong> Data { get; set; }
    }
}
