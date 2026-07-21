using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class CommentResult : BaseResult
    {
        [JsonProperty("data")]
        public CommentResultData Data { get; set; }
    }
}
