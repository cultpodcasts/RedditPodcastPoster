using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WikiPageListing : BaseContainer
    {
        [JsonProperty("data")]
        public List<string> Data { get; set; }
    }
}
