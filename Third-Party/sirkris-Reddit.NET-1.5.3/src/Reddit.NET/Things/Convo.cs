using System;
using Newtonsoft.Json;
using Reddit.Models.Converters;

namespace Reddit.Things
{
    [Serializable]
    public class Convo
    {
        [JsonProperty("date")]
        [JsonConverter(typeof(UtcTimestampConverter))]
        public DateTime Date { get; set; }

        [JsonProperty("permalink")]
        public string Permalink { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("subject")]
        public string Subject { get; set; }
    }
}
