using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class FlairRichtext
    {
        [JsonProperty("e")]
        public string E { get; set; }

        [JsonProperty("t")]
        public string T { get; set; }
    }
}
