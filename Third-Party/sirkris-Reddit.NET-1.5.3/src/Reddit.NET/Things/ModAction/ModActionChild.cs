using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ModActionChild : BaseContainer
    {
        [JsonProperty("data")]
        public ModAction Data { get; set; }
    }
}
