using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostChild : BaseContainer
    {
        [JsonProperty("data")]
        public Post Data { get; set; }
    }
}
