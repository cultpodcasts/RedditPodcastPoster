using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetImage : BaseContainer
    {
        [JsonProperty("data")]
        public List<WidgetImageData> Data { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("styles")]
        public WidgetStyles Styles { get; set; }
    }
}
