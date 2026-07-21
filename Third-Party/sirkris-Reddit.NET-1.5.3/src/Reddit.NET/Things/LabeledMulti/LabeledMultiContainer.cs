using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LabeledMultiContainer : BaseContainer
    {
        [JsonProperty("data")]
        public LabeledMulti Data { get; set; }
    }
}
