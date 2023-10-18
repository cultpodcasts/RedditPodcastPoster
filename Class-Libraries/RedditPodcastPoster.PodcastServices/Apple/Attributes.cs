using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class Attributes
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("releaseDateTime")]
    public DateTime Released { get; set; }

    [JsonPropertyName("durationInMilliseconds")]
    public long LengthMs { get; set; }

    [JsonPropertyName("description")]
    public Description Description { get; set; } = new Description();

    [JsonPropertyName("contentAdvisory")]
    public string ContentAdvisory { get; set; } = string.Empty;

    public bool Explicit => !string.IsNullOrWhiteSpace(ContentAdvisory);

    public TimeSpan Duration => TimeSpan.FromMilliseconds(LengthMs);
}