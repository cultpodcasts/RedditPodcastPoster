using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public static class FullEpisodeExtensions
{
    public static Uri GetUrl(this FullEpisode fullEpisode)
    {
        return new Uri(fullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }
}