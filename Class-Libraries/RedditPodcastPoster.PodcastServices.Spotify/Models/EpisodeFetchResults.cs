using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

internal record EpisodeFetchResults(string SpotifyPodcastId, Paging<SimpleEpisode>? Episodes);