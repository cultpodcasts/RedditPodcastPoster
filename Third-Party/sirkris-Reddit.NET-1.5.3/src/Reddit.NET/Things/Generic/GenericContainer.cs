using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class GenericContainer
    {
        [JsonProperty("json")]
        public Generic JSON { get; set; }
    }
}
