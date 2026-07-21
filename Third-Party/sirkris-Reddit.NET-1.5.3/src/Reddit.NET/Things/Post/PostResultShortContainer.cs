using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostResultShortContainer
    {
        [JsonProperty("json")]
        public PostResultShort JSON { get; set; }
    }
}
