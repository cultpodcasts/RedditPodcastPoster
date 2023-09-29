namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record SpotifyGetEpisodesRequest(string SpotifyId, DateTime? ProcessRequestReleasedSince);