using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetButton3Data
    {
        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("hoverState")]
        public WidgetHoverState HoverState { get; set; }
    }
}
