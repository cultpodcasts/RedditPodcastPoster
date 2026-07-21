using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostResult : BaseResult
    {
        [JsonProperty("data")]
        public PostResultData Data { get; set; }
    }
}
