using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class MultipleResponseContainer
    {
        [JsonProperty("json")]
        public MultipleResponse JSON { get; set; }
    }
}
