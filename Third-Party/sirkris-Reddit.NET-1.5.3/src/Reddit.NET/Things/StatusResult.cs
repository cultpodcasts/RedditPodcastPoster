using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class StatusResult
    {
        [JsonProperty("status")]
        public bool Status { get; set; }
    }
}
