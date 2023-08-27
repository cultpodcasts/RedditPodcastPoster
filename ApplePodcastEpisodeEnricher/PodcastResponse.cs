using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ApplePodcastEpisodeEnricher
{
    public class PodcastResponse
    {
        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("data")]
        public List<Record> Records { get; set; }
    }
}

public class Record
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("attributes")]
    public Attributes Attributes { get; set; }
}

public class Attributes
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("releaseDateTime")]
    public DateTime Released { get; set; }

    [JsonPropertyName("durationInMilliseconds")]
    public long LengthMs { get; set; }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(LengthMs);

}