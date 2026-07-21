using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetButton2 : WidgetButton
    {
        [JsonProperty("buttons")]
        public List<WidgetButton2Data> Buttons { get; set; }
    }
}
