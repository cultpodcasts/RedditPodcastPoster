using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class Generic : BaseResult
    {
        [JsonProperty("data")]
        public dynamic Data { get; set; }
    }
}
