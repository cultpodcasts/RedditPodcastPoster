using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class EpisodeExtensions
{
    extension(FullEpisode fullEpisode)
    {
        public Uri GetUrl()
        {
            return new Uri(fullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
        }

        public Uri? GetBestImageUrl()
        {
            var bestImage = fullEpisode.Images.MaxBy(x => x.Height);
            if (bestImage == null)
            {
                return null;
            }

            return new Uri(bestImage.Url);
        }
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