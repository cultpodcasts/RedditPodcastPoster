using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserContainer : BaseContainer
    {
        [JsonProperty("data")]
        public UserData Data { get; set; }
    }
}
