using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Models;

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

    /// <summary>
    /// Option B for #1200: map a curator-confirmed kind candidate to submit options.
    /// Returns null when the key is missing or not recognised. A caller that already received <c>select</c> must not treat null as "keep as podcast".
    /// Nothing is applied until the caller writes this onto SubmitOptions.
    /// </summary>
    public static DiscoveryKindApplication? FromDiscovery(Uri url, string? selectedKey)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (string.IsNullOrWhiteSpace(selectedKey))
        {
            return null;
        }

        if (DiscoveryKindKeys.TryParseFilm(selectedKey, out var filmId))
        {
            return new DiscoveryKindApplication(
                new SubmitClassificationSignals(url, MadeAsFilm: true),
                TargetFilmId: filmId);
        }

        if (DiscoveryKindKeys.TryParseTvShow(selectedKey, out var showId))
        {
            return new DiscoveryKindApplication(
                new SubmitClassificationSignals(url, Series: true),
                Parent: new CatalogueParentTarget(CatalogueParentKind.TvShow, showId));
        }

        if (DiscoveryKindKeys.TryParseNewsReport(selectedKey, out var organisationId, out var reportId))
        {
            return new DiscoveryKindApplication(
                new SubmitClassificationSignals(url, News: true),
                Parent: new CatalogueParentTarget(CatalogueParentKind.NewsOrganisation, organisationId),
                TargetNewsOrganisationId: organisationId,
                TargetNewsReportId: reportId);
        }

        if (CatalogueAuthorityCandidate.TryParseKey(selectedKey, out var tmdbId, out _) && tmdbId is > 0)
        {
            return new DiscoveryKindApplication(
                new SubmitClassificationSignals(url, MadeAsFilm: true),
                IdentityChoice: CatalogueIdentityChoice.Select,
                SelectedIdentityKey: selectedKey);
        }

        if (CatalogueAuthorityCandidate.TryParseImdbKey(selectedKey, out _))
        {
            return new DiscoveryKindApplication(
                new SubmitClassificationSignals(url, MadeAsFilm: true),
                IdentityChoice: CatalogueIdentityChoice.Select,
                SelectedIdentityKey: selectedKey);
        }

        return null;
    }
    public static SubmitClassification Classify(SubmitClassificationSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        // CLS-002: a Netflix /title/ page for a series is a catalogue hub (no TvShow stub), so it is rejected.
        // #1517: a /title/ page whose primary type is Movie is the film's own page (films have no stable
        // public watch URL), so it falls through to Film below.
        if (IsNetflixTitleHub(signals.Url) && !IsStandaloneFilm(signals))
        {
            return new SubmitClassification(
                SubmitClassification.TvShowEpisode,
                HasParent: false,
                Reject: true,
                RequiresCurator: false,
                RejectionMessage: NetflixSeriesHubSubmitRejection.Message);
        }

        // #1587: Discovery+ /show/{id} hubs do not expand episode lists; reject like Netflix series hubs.
        if (DiscoveryPlusUrlMatcher.IsShowHub(signals.Url) && !IsStandaloneFilm(signals))
        {
            return new SubmitClassification(
                SubmitClassification.TvShowEpisode,
                HasParent: false,
                Reject: true,
                RequiresCurator: false,
                RejectionMessage: DiscoveryPlusSeriesHubSubmitRejection.Message);
        }

        if (IsBbcNews(signals.Url) || signals.YouTubeNewsStation || signals.News)
        {
            return new SubmitClassification(
                SubmitClassification.NewsReport,
                HasParent: true,
                Reject: false,
                RequiresCurator: false);
        }

        if (IsBbcSoundsPlay(signals.Url))
        {
            return SubmitClassification.PodcastEpisode();
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

    private static bool IsStandaloneFilm(SubmitClassificationSignals signals) =>
        signals.MadeAsFilm && !signals.Series;

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

    /// <summary>
    /// BBC Sounds play pages are audio catalogue. A scraped series name must not
    /// file them as TvShow episodes when the curator is attaching to a podcast.
    /// </summary>
    public static bool IsBbcSoundsPlay(Uri url) => BBCUrlMatcher.IsSoundsPlayUrl(url);

    /// <summary>
    /// BBC iPlayer episode pages (not Sounds, not /news/). Used for stored TV identify without scrape.
    /// </summary>
    public static bool IsBbcIplayerEpisode(Uri url)
    {
        if (!url.IsAbsoluteUri || (!HostIs(url, "bbc.co.uk") && !HostIs(url, "bbc.com")))
        {
            return false;
        }

        var path = url.AbsolutePath;
        return path.Equals("/iplayer/episode", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/iplayer/episode/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HostIs(Uri url, string host)
    {
        var value = url.Host;
        return value.Equals(host, StringComparison.OrdinalIgnoreCase)
               || value.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);
    }
}
