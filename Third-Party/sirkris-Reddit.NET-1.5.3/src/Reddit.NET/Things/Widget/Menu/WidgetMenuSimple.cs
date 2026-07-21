using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetMenuSimple : BaseContainer
    {
        [JsonProperty("data")]
        public List<WidgetMenuData> Data { get; set; }
    }
}
