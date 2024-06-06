using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Converters;

namespace RedditPodcastPoster.Models;

public class DiscoveryResult
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("episodeName")]
    [JsonPropertyOrder(20)]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("showName")]
    [JsonPropertyOrder(30)]
    public string? ShowName { get; set; }

    [JsonPropertyName("released")]
    [JsonPropertyOrder(40)]
    public DateTime Released { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(50)]
    public TimeSpan? Length { get; set; }

    [JsonPropertyName("showDescription")]
    [JsonPropertyOrder(60)]
    public string? ShowDescription { get; set; }

    [JsonPropertyName("episodeDescription")]
    [JsonPropertyOrder(70)]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(80)]
    public DiscoveryResultState? State { get; set; } = null;

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(90)]
    public DiscoveryResultUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(100)]
    public IEnumerable<string> Subjects { get; set; } = [];

    [JsonPropertyName("youTubeViews")]
    [JsonPropertyOrder(110)]
    public ulong? YouTubeViews { get; set; }

    [JsonPropertyName("youTubeChannelMembers")]
    [JsonPropertyOrder(120)]
    public ulong? YouTubeChannelMembers { get; set; }

    [JsonPropertyName("imageUrl")]
    [JsonPropertyOrder(130)]
    public Uri? ImageUrl { get; set; }

    [JsonPropertyName("discoverService")]
    [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
    [JsonPropertyOrder(140)]
    public DiscoverService[] Sources { get; set; } = [];

    [JsonPropertyName("enrichedTimeFromApple")]
    [JsonPropertyOrder(150)]
    public bool EnrichedTimeFromApple { get; set; }

    [JsonPropertyName("enrichedUrlFromSpotify")]
    [JsonPropertyOrder(160)]
    public bool EnrichedUrlFromSpotify { get; set; }

    [JsonPropertyName("matchingPodcastIds")]
    [JsonPropertyOrder(170)]
    public Guid[] MatchingPodcastIds { get; set; } = [];
}