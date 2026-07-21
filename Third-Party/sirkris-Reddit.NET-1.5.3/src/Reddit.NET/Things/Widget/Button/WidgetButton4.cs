using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetButton4 : WidgetButton
    {
        [JsonProperty("buttons")]
        public List<WidgetButton4Data> Buttons { get; set; }
    }
}
