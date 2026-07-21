using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MessageChild : BaseContainer
    {
        [JsonProperty("data")]
        public Message Data { get; set; }
    }
}
