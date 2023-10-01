namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindSpotifyPodcastRequest(
    string PodcastId,
    string Name,
    IList<FindSpotifyPodcastRequestEpisodes> Episodes);