using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeWrapper
{
    public FullEpisode? FullEpisode { get; init; }

    public SpotifyEpisodeWrapper(FullEpisode? fullEpisode)
    {
        FullEpisode = fullEpisode;
    }

    public Uri Url()
    {
        return new Uri(FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }
}