using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class JQueryReturn
    {
        [JsonProperty("jquery")]
        public dynamic JQuery { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}
