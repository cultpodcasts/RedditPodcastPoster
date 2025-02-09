namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record GetEpisodesRequest(
    SpotifyPodcastId SpotifyPodcastId,
    string? Market,
    bool HasExpensiveSpotifyEpisodesQuery = false);