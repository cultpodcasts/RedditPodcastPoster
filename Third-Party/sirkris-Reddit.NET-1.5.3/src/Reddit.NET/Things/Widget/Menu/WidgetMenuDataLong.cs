using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetMenuDataLong
    {
        [JsonProperty("children")]
        public List<WidgetMenuData> Children { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
