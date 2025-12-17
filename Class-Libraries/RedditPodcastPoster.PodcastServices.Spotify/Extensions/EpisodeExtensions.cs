using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class EpisodeExtensions
{
    public static Uri GetUrl(this FullEpisode fullEpisode)
    {
        return new Uri(fullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }

    public static Uri? GetBestImageUrl(this FullEpisode episode)
    {
        var bestImage = episode.Images.MaxBy(x => x.Height);
        if (bestImage == null)
        {
            return null;
        }

        return new Uri(bestImage.Url);
    }

    public static Uri? GetBestImageUrl(this SimpleEpisode episode)
    {
        var bestImage = episode.Images.MaxBy(x => x.Height);
        if (bestImage == null)
        {
            return null;
        }

        return new Uri(bestImage.Url);
    }
}