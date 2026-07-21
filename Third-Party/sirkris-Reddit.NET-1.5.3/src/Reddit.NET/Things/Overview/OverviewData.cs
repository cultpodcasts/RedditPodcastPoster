using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Reddit.Models.Converters;

namespace Reddit.Things
{
    [Serializable]
    public class OverviewData : BaseData
    {
        [JsonProperty("children")]
        [JsonConverter(typeof(UserOverviewConverter))]
        public List<CommentOrPost> Children { get; set; }
    }
}
