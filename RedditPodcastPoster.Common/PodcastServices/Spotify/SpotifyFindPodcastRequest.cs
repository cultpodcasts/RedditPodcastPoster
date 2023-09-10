namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record SpotifyFindPodcastRequest(string SpotifyId, string Name,
    IList<SpotifyFindPodcastRequestEpisodes> Episodes);