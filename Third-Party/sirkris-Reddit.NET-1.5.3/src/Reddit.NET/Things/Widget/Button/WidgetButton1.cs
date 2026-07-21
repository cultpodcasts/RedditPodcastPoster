using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetButton1 : WidgetButton
    {
        [JsonProperty("buttons")]
        public List<WidgetButton1Data> Buttons { get; set; }
    }
}
