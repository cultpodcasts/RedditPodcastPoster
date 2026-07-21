using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ModActionData : BaseData
    {
        [JsonProperty("children")]
        public List<ModActionChild> Children { get; set; }
    }
}
