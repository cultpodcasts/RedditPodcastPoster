using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeWrapper
{
    public SpotifyEpisodeWrapper(FullEpisode? fullEpisode)
    {
        FullEpisode = fullEpisode;
    }

    public FullEpisode? FullEpisode { get; init; }

    public Uri? Url()
    {
        if (FullEpisode == null)
        {
            return null;
        }

        return new Uri(FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }
}