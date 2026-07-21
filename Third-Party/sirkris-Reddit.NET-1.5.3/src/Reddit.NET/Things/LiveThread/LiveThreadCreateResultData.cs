using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LiveThreadCreateResultData
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
