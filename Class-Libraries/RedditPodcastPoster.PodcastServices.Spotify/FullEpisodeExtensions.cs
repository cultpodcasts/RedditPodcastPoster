using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public static class FullEpisodeExtensions
{
    public static Uri GetUrl(this FullEpisode fullEpisode)
    {
        return new Uri(Enumerable.FirstOrDefault<KeyValuePair<string, string>>(fullEpisode.ExternalUrls).Value, UriKind.Absolute);
    }
}