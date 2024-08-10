using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record FindSpotifyEpisodeRequest(
    string PodcastSpotifyId,
    string PodcastName,
    string EpisodeSpotifyId,
    string EpisodeTitle,
    DateTime? Released,
    bool HasExpensiveSpotifyEpisodesQuery,
    TimeSpan? YouTubePublishingDelay= null,
    Service? ReleaseAuthority = null,
    TimeSpan? Length = null,
    string? Market = null);