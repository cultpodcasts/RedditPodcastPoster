using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class CommentResultData
    {
        [JsonProperty("things")]
        public List<CommentChild> Things { get; set; }
    }
}
