using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class CommentContainer : BaseContainer
    {
        [JsonProperty("data")]
        public CommentData Data { get; set; }
    }
}
