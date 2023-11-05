using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record FindSpotifyEpisodeRequest(
    string PodcastSpotifyId,
    string PodcastName,
    string EpisodeSpotifyId,
    string EpisodeTitle,
    DateTime? Released,
    bool HasExpensiveSpotifyEpisodesQuery,
    Service? ReleaseAuthority = null,
    TimeSpan? Length = null,
    string? Market = null);