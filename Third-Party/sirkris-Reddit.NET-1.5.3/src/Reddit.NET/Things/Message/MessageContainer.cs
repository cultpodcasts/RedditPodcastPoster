using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MessageContainer : BaseContainer
    {
        [JsonProperty("data")]
        public MessageData Data { get; set; }
    }
}
