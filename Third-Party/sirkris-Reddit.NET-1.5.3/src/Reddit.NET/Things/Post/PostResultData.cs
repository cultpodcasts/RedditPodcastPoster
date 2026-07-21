using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class PostResultData
    {
        [JsonProperty("things")]
        public List<PostChild> Things { get; set; }
    }
}
