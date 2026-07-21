using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MoreChild : BaseContainer
    {
        [JsonProperty("data")]
        public More Data { get; set; }
    }
}
