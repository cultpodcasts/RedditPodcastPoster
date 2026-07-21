using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostResultShort : BaseResult
    {
        [JsonProperty("data")]
        public PostResultShortData Data { get; set; }
    }
}
