using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LiveUpdateContainer : BaseContainer
    {
        [JsonProperty("data")]
        public LiveUpdateData Data { get; set; }
    }
}
