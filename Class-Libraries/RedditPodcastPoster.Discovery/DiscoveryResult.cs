using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResult
{
    [JsonPropertyName("url")]
    public Uri? Url { get; set; }

    [JsonPropertyName("episodeName")]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("showName")]
    public string? ShowName { get; set; }

    [JsonPropertyName("episodeDescription")]
    public string? Description { get; set; }

    [JsonPropertyName("released")]
    public DateTime Released { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Length { get; set; }

    [JsonPropertyName("subjects")]
    public IEnumerable<string> Subjects { get; set; } = Enumerable.Empty<string>();

    [JsonPropertyName("youTubeViews")]
    public ulong? YouTubeViews { get; set; }

    [JsonPropertyName("youTubeChannelMembers")]
    public ulong? YouTubeChannelMembers { get; set; }
}