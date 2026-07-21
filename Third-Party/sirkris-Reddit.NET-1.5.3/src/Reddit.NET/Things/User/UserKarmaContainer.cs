using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserKarmaContainer
    {
        [JsonProperty("data")]
        public List<UserKarma> Data { get; set; }
    }
}
