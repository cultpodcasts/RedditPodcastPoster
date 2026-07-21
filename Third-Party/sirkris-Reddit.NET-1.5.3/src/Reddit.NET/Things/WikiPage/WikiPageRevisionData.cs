using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WikiPageRevisionData : BaseData
    {
        [JsonProperty("children")]
        public List<WikiPageRevision> Children { get; set; }
    }
}
