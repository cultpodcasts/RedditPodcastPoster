using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record EpisodeFetchResults(string SpotifyPodcastId, Paging<SimpleEpisode>? Episodes);