using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LiveUpdateEventContainer : BaseContainer
    {
        [JsonProperty("data")]
        public LiveUpdateEvent Data { get; set; }
    }
}
