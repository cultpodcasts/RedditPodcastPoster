using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// ADR-0004 order for submit v2. When the feature flag is off, callers must use
/// <see cref="WhenEnabled"/> so persist stays Podcast + Episode.
/// </summary>
public static class SubmitContentClassifier
{
    public static SubmitClassificationSignals FromSubmission(Uri url, CategorisedItem item)
    {
        var resolved = item.ResolvedNonPodcastServiceItem;
        var madeAsFilm = resolved?.MadeAsFilm == true;
        var series = resolved != null && !string.IsNullOrWhiteSpace(resolved.ShowName);
        var podcastServiceEpisode = resolved is null && item.Authority is Service.Spotify or Service.Apple or Service.YouTube;
        return new SubmitClassificationSignals(
            url,
            PodcastServiceEpisode: podcastServiceEpisode,
            MadeAsFilm: madeAsFilm,
            Series: series);
    }

    public static SubmitClassification Classify(SubmitClassificationSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (IsNetflixTitleHub(signals.Url))
        {
            return new SubmitClassification(
                SubmitClassification.Episode,
                HasParent: false,
                Reject: true,
                RequiresCurator: false);
        }

        if (IsBbcNews(signals.Url) || signals.YouTubeNewsStation || signals.News)
        {
            return new SubmitClassification(
                SubmitClassification.NewsReport,
                HasParent: true,
                Reject: false,
                RequiresCurator: false);
        }

        if (signals.Series)
        {
            return new SubmitClassification(
                SubmitClassification.TvShowEpisode,
                HasParent: true,
                Reject: false,
                RequiresCurator: false);
        }

        if (signals.MadeAsFilm)
        {
            return new SubmitClassification(
                SubmitClassification.Film,
                HasParent: false,
                Reject: false,
                RequiresCurator: false);
        }

        if (IsVimeo(signals.Url) && !signals.CuratorConfirmed)
        {
            return new SubmitClassification(
                SubmitClassification.Episode,
                HasParent: true,
                Reject: false,
                RequiresCurator: true);
        }

        return SubmitClassification.PodcastEpisode();
    }

    /// <summary>
    /// Flag off keeps today's Podcast + Episode persist, including for a rejected or curator URL.
    /// </summary>
    public static SubmitClassification WhenEnabled(SubmitClassification classified, bool enabled) =>
        enabled ? classified : SubmitClassification.PodcastEpisode();

    public static bool IsNetflixTitleHub(Uri url)
    {
        if (!HostIs(url, "netflix.com"))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Contains("/title/", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("/watch/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVimeo(Uri url) => HostIs(url, "vimeo.com");

    /// <summary>
    /// BBC news pages are not iPlayer or Sounds. They stay non-submit for the podcast path.
    /// </summary>
    public static bool IsBbcNews(Uri url)
    {
        if (!url.IsAbsoluteUri || (!HostIs(url, "bbc.co.uk") && !HostIs(url, "bbc.com")))
        {
            return false;
        }

        return url.AbsolutePath.StartsWith("/news/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HostIs(Uri url, string host)
    {
        var value = url.Host;
        return value.Equals(host, StringComparison.OrdinalIgnoreCase)
               || value.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);
    }
}
