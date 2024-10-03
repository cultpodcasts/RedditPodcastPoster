namespace RedditPodcastPoster.PodcastServices.Spotify;

public record GetEpisodesRequest(
    SpotifyPodcastId SpotifyPodcastId,
    string? Market,
    bool HasExpensiveSpotifyEpisodesQuery = false);