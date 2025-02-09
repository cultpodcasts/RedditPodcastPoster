namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record FindSpotifyPodcastRequestEpisodes(DateTime Release, Uri? Url, string Title);