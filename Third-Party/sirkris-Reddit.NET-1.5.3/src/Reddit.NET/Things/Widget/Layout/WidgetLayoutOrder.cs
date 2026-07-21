using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WidgetLayoutOrder
    {
        [JsonProperty("order")]
        public List<string> Order { get; set; }
    }
}
