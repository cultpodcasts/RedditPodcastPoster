using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MoreContainer : BaseContainer
    {
        [JsonProperty("data")]
        public MoreData Data { get; set; }
    }
}
