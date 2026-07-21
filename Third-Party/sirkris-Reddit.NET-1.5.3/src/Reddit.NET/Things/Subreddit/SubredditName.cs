using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class SubredditName
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        public SubredditName(string name)
        {
            Name = name;
        }

        public SubredditName() { }
    }
}
