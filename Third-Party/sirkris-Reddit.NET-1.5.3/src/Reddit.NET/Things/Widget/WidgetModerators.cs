using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetModerators : BaseContainer
    {
        [JsonProperty("styles")]
        public WidgetStyles Styles { get; set; }
    }
}
