using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public abstract class BaseContainer
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }
    }
}
