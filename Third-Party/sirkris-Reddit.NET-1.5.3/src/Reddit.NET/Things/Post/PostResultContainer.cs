using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostResultContainer
    {
        [JsonProperty("json")]
        public PostResult JSON { get; set; }
    }
}
