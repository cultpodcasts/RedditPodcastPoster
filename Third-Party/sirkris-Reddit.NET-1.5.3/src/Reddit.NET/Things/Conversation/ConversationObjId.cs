using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ConversationObjId
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
    }
}
