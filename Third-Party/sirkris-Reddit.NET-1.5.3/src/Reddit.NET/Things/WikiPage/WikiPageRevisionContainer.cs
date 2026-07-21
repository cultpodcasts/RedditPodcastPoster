using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WikiPageRevisionContainer : BaseContainer
    {
        [JsonProperty("data")]
        public WikiPageRevisionData Data { get; set; }
    }
}
