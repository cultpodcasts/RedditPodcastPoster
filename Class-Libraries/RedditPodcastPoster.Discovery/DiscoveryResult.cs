using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResult
{
    [JsonPropertyName("url")]
    [JsonPropertyOrder(10)]
    public Uri? Url { get; set; }

    [JsonPropertyName("episodeName")]
    [JsonPropertyOrder(20)]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("showName")]
    [JsonPropertyOrder(30)]
    public string? ShowName { get; set; }

    [JsonPropertyName("episodeDescription")]
    [JsonPropertyOrder(40)]
    public string? Description { get; set; }

    [JsonPropertyName("released")]
    [JsonPropertyOrder(50)]
    public DateTime Released { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(60)]
    public TimeSpan? Length { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public IEnumerable<string> Subjects { get; set; } = Enumerable.Empty<string>();

    [JsonPropertyName("youTubeViews")]
    [JsonPropertyOrder(80)]
    public ulong? YouTubeViews { get; set; }

    [JsonPropertyName("youTubeChannelMembers")]
    [JsonPropertyOrder(90)]
    public ulong? YouTubeChannelMembers { get; set; }

    [JsonPropertyName("imageUrl")]
    [JsonPropertyOrder(100)]
    public Uri? ImageUrl { get; set; }
}