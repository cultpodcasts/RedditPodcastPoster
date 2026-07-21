using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WikiPageContainer : BaseContainer
    {
        [JsonProperty("data")]
        public WikiPage Data { get; set; }
    }
}
