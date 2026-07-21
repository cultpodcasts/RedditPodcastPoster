using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class ModActionContainer : BaseContainer
    {
        [JsonProperty("data")]
        public ModActionData Data { get; set; }
    }
}
