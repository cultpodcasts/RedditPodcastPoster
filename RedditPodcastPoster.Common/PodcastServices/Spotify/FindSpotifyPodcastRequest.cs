namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindSpotifyPodcastRequest(string SpotifyId, string Name,
    IList<FindSpotifyPodcastRequestEpisodes> Episodes);