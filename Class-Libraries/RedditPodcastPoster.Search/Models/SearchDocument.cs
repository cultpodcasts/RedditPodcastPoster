using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Search.Models;

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
        var episode = new Episode
        {
            Id = Id,
            Title = EpisodeTitle,
            Description = EpisodeDescription,
            Subjects = Subjects.ToList(),
            Length = length,
            Release = Release ?? DateTime.MinValue
        };
        EpisodeServicePresence.SetSpotifyIdentity(episode, SpotifyId);
        EpisodeServicePresence.SetYouTubeIdentity(episode, YoutubeId);
        if (long.TryParse(AppleId, out var appleId))
        {
            EpisodeServicePresence.SetAppleIdentity(episode, appleId);
        }

        return episode;
    }
}
