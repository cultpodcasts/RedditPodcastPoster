using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MultipleResponse : BaseResult
    {
        [JsonProperty("data")]
        public MultipleResponseData Data { get; set; }
    }
}