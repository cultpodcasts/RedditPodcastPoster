namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record GetSpotifyPodcastEpisodesRequest(string SpotifyPodcastId, DateTime? ReleasedSince);