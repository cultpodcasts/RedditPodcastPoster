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
    public Description Description { get; set; } = new();

    [JsonPropertyName("contentAdvisory")]
    public string ContentAdvisory { get; set; } = string.Empty;

    [JsonPropertyName("artwork")]
    public Artwork? Artwork { get; set; } = null;

    public bool Explicit => !string.IsNullOrWhiteSpace(ContentAdvisory);

    public TimeSpan Duration => TimeSpan.FromMilliseconds(LengthMs);

    public Uri? Image()
    {
        if (Artwork == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(Artwork.Url) || Artwork.Height == null || Artwork.Height == 0 ||
            Artwork.Width == null || Artwork.Width == 0)
        {
            return null;
        }

        return new Uri(Artwork.Url.Replace("{w}", Artwork.Width.ToString()).Replace("{h}", Artwork.Height.ToString())
            .Replace("{f}", "png"));
    }
}