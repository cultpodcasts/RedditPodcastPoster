using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Search;

public class SearchDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastName")]
    public string? PodcastName { get; set; }

    [JsonPropertyName("episodeTitle")]
    public string EpisodeTitle { get; set; } = null!;

    [JsonPropertyName("episodeDescription")]
    public string EpisodeDescription { get; set; } = null!;

    [JsonPropertyName("subjects")]
    public string[] Subjects { get; set; } = null!;

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("release")]
    public DateTime? Release { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("youtubeId")]
    public string? YoutubeId { get; set; }

    [JsonPropertyName("appleId")]
    public string? AppleId { get; set; }

    [JsonPropertyName("podcastAppleId")]
    public string? PodcastAppleId { get; set; }

    public Episode ToEpisodeModel()
    {
        var length = !string.IsNullOrWhiteSpace(Duration) ? TimeSpan.Parse(Duration) : TimeSpan.Zero;
        return new Episode
        {
            Id = Id,
            Title = EpisodeTitle,
            Description = EpisodeDescription,
            Subjects = Subjects.ToList(),
            Length = length,
            Release = Release ?? DateTime.MinValue,
            SpotifyId = SpotifyId ?? string.Empty,
            YouTubeId = YoutubeId ?? string.Empty,
            AppleId = long.TryParse(AppleId, out var appleId) ? appleId : null
        };
    }
}
