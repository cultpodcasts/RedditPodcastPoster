using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ModeratedListContainer : BaseContainer
    {
        [JsonProperty("data")]
        public List<ModeratedListItem> Data { get; set; }
    }
}
