using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetImageData
    {
        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("linkUrl")]
        public string LinkURL { get; set; }

        [JsonProperty("url")]
        public string URL { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }
    }
}
