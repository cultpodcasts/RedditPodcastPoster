using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class UserPrefsData
    {
        [JsonProperty("children")]
        public List<UserPrefs> Children { get; set; }
    }
}
