using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class TrophyList : BaseContainer
    {
        [JsonProperty("data")]
        public TrophiesData Data { get; set; }
    }
}
