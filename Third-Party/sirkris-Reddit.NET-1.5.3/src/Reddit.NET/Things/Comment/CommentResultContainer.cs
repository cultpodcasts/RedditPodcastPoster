using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class CommentResultContainer
    {
        [JsonProperty("json")]
        public CommentResult JSON { get; set; }
    }
}
