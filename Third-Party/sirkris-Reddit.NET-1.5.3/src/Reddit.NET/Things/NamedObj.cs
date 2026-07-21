using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class NamedObj
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
