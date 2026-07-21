using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetResults
    {
        [JsonProperty("items")]
        public Dictionary<string, dynamic> Items { get; set; }

        [JsonProperty("layout")]
        public WidgetLayout Layout { get; set; }
    }
}
