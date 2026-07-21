using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetButton3 : WidgetButton
    {
        [JsonProperty("buttons")]
        public List<WidgetButton3Data> Buttons { get; set; }
    }
}
