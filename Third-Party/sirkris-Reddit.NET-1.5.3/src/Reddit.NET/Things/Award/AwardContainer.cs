using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class AwardContainer : BaseContainer
    {
        [JsonProperty("data")]
        public Award Data { get; set; }
    }
}
