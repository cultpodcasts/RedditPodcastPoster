namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record FindSpotifyPodcastRequest(
    string PodcastId,
    string Name,
    IList<FindSpotifyPodcastRequestEpisodes> Episodes);