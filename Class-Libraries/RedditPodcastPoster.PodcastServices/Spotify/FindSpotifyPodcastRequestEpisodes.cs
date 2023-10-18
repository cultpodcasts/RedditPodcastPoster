namespace RedditPodcastPoster.PodcastServices.Spotify;

public record FindSpotifyPodcastRequestEpisodes(DateTime Release, Uri? Url, string Title);