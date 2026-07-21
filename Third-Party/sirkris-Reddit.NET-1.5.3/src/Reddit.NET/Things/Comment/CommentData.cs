using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class CommentData : BaseData
    {
        [JsonProperty("children")]
        public List<CommentChild> Children { get; set; }
    }
}
