using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MoreSimpleContainer : BaseContainer
    {
        [JsonProperty("data")]
        public More Data { get; set; }
    }
}
