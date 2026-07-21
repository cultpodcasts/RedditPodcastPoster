using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetMenuData
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("url")]
        public string URL { get; set; }
    }
}
