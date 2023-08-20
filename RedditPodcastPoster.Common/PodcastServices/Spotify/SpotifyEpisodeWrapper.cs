using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeWrapper
{
    public FullEpisode? FullEpisode { get; init; }
    public SimpleEpisode? SimpleEpisode { get; init; }

    public SpotifyEpisodeWrapper(FullEpisode? fullEpisode = null, SimpleEpisode? simpleEpisode = null)
    {
        FullEpisode = fullEpisode;
        SimpleEpisode = simpleEpisode;
    }

    public string Id => FullEpisode?.Id ?? SimpleEpisode?.Id ?? string.Empty;

    public Uri? Url()
    {
        if (FullEpisode == null && SimpleEpisode == null) return null;
        if (FullEpisode != null)
        {
            return new Uri(FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
        }

        return new Uri(SimpleEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }
}