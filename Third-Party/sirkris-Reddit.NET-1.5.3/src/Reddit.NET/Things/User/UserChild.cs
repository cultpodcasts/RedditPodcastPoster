using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserChild : BaseContainer
    {
        [JsonProperty("data")]
        public User Data { get; set; }
    }
}
