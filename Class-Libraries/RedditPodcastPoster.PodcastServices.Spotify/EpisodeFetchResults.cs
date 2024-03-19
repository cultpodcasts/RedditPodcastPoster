using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

internal record EpisodeFetchResults(string SpotifyPodcastId, Paging<SimpleEpisode>? Episodes);