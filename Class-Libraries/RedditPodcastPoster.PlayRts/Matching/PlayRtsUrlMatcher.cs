using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Matching;

public static class PlayRtsUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        if (!ServiceCatalog.IsHost(ServiceCatalog.CanonicalHost(url), "rts.ch"))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !parts[0].Equals("play", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var medium = parts[1];
        if (!medium.Equals("tv", StringComparison.OrdinalIgnoreCase) &&
            !medium.Equals("radio", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        if (parts.Length == 3)
        {
            return true;
        }

        return IsEpisodePath(url);
    }

    public static bool IsEpisodePath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 5 &&
               (parts[3].Equals("video", StringComparison.OrdinalIgnoreCase) ||
                parts[3].Equals("audio", StringComparison.OrdinalIgnoreCase)) &&
               !string.IsNullOrWhiteSpace(parts[4]);
    }
}
