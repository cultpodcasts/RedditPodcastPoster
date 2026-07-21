using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserPrefsContainer : BaseContainer
    {
        [JsonProperty("data")]
        public UserPrefsData Data { get; set; }
    }
}
