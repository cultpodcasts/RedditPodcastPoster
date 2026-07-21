using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MessageData : BaseData
    {
        [JsonProperty("children")]
        public List<MessageChild> Children { get; set; }
    }
}
