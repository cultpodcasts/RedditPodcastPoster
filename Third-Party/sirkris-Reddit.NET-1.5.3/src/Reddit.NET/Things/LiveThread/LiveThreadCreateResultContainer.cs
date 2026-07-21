using System;
using Newtonsoft.Json;

namespace Reddit.Things
{
    [Serializable]
    public class LiveThreadCreateResultContainer
    {
        [JsonProperty("json")]
        public LiveThreadCreateResult JSON { get; set; }
    }
}
