using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class WikiPageSettingsContainer : BaseContainer
    {
        [JsonProperty("data")]
        public WikiPageSettings Data { get; set; }
    }
}
