using System.Net;
using System.Text.RegularExpressions;
using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Channel4.Matching;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.DisneyPlus.Matching;
using RedditPodcastPoster.Fawesome.Matching;
using RedditPodcastPoster.HboMax.Matching;
using RedditPodcastPoster.Itvx.Matching;
using RedditPodcastPoster.Netflix.Matching;
using RedditPodcastPoster.ParamountPlus.Matching;
using RedditPodcastPoster.PlaySuisse.Matching;
using RedditPodcastPoster.TvnzPlus.Matching;
using RedditPodcastPoster.Vimeo.Matching;

namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Live browse (homepage / section) pages used to harvest submit-eligible streaming URLs.
/// Homepages themselves are not submit URLs — we only assert on links that match each provider matcher.
/// </summary>
public static class StreamingScraperBrowsePages
{
    public static TheoryData<StreamingScraperBrowsePage> All() => new(Pages);

    public static IReadOnlyList<StreamingScraperBrowsePage> Pages { get; } =
    [
        // Floors / SampleLookups are intentionally low: this suite is opt-in drift detection
        // (skipped in CI via SKIP_LIVE_STREAMING_SCRAPER_TESTS=1), not a storefront card-count contract.
        new(StreamingScraperProvider.BbcSounds, "sounds-home",
            new Uri("https://www.bbc.co.uk/sounds"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Sounds homepage SSR embeds /sounds/play/ programme cards"),
        new(StreamingScraperProvider.BbcSounds, "sounds-music",
            new Uri("https://www.bbc.co.uk/sounds/music"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Sounds music section lists playable programmes"),
        new(StreamingScraperProvider.BbcSounds, "sounds-podcasts",
            new Uri("https://www.bbc.co.uk/sounds/podcasts"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Sounds podcasts section lists playable programmes"),

        new(StreamingScraperProvider.BbcIplayer, "iplayer-home",
            new Uri("https://www.bbc.co.uk/iplayer"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "iPlayer homepage embeds /iplayer/episode/ deep links"),
        new(StreamingScraperProvider.BbcIplayer, "iplayer-films-az",
            new Uri("https://www.bbc.co.uk/iplayer/categories/films/a-z"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "iPlayer films A–Z section is rich in episode (incl. film) URLs"),

        // Netflix marketing/home SSR does not reliably expose /title/ hrefs; keep title catalogue
        // coverage in StreamingScraperCanonicalCases instead of browse harvest.

        new(StreamingScraperProvider.AmazonPrime, "prime-storefront-home",
            new Uri("https://www.primevideo.com/storefront/home"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Prime storefront home SSR embeds /detail/ title cards"),

        new(StreamingScraperProvider.Vimeo, "vimeo-home",
            new Uri("https://vimeo.com/"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Vimeo homepage may expose a small number of numeric video ids"),

        new(StreamingScraperProvider.Channel4, "channel4-home",
            new Uri("https://www.channel4.com/"),
            MinSubmitLinks: 1,
            SampleLookups: 1,
            StabilityNote: "Channel 4 homepage SSR embeds /programmes/ catalogue cards"),

        // Fawesome / Disney+ / discovery+ / Max / Play Suisse / ITVX / TVNZ+ / Paramount+
        // marketing or geo-walled shells do not reliably SSR submit deep links; keep coverage
        // in StreamingScraperCanonicalCases instead of browse harvest.
    ];
}

public sealed record StreamingScraperBrowsePage(
    StreamingScraperProvider Provider,
    string CaseId,
    Uri BrowseUrl,
    int MinSubmitLinks,
    int SampleLookups,
    string StabilityNote)
{
    public override string ToString() => $"{Provider}/{CaseId}";
}

internal static partial class StreamingScraperBrowseLinkHarvester
{
    private static readonly HttpClient SharedClient = CreateClient();

    public static async Task<IReadOnlyList<Uri>> HarvestSubmitUrlsAsync(
        StreamingScraperBrowsePage page,
        CancellationToken cancellationToken)
    {
        using var response = await SharedClient.GetAsync(page.BrowseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var baseUri = page.BrowseUrl;

        var hrefs = HrefRegex().Matches(html)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(href => TryCreateAbsolute(baseUri, href))
            .Where(uri => uri != null)
            .Cast<Uri>()
            .Where(uri => IsSubmitUrl(page.Provider, uri))
            .GroupBy(uri => NormalizeKey(page.Provider, uri), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return hrefs;
    }

    public static bool IsSubmitUrl(StreamingScraperProvider provider, Uri url) =>
        provider switch
        {
            StreamingScraperProvider.BbcSounds => BBCUrlMatcher.IsSoundsPlayUrl(url),
            StreamingScraperProvider.BbcIplayer => BBCUrlMatcher.IsIplayerEpisodeUrl(url),
            StreamingScraperProvider.Netflix => NetflixUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.AmazonPrime => AmazonPrimeUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.Vimeo => VimeoUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.Itvx => ItvxUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.Channel4 => Channel4UrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.Fawesome => FawesomeUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.ParamountPlus => ParamountPlusUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.HboMax => HboMaxUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.PlaySuisse => PlaySuisseUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.TvnzPlus => TvnzPlusUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.DisneyPlus => DisneyPlusUrlMatcher.IsSubmitUrl(url),
            StreamingScraperProvider.DiscoveryPlus => DiscoveryPlusUrlMatcher.IsSubmitUrl(url),
            _ => false
        };

    private static Uri? TryCreateAbsolute(Uri baseUri, string href)
    {
        if (string.IsNullOrWhiteSpace(href) ||
            href.StartsWith('#') ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Uri.TryCreate(baseUri, href, out var absolute) ? absolute : null;
    }

    private static string NormalizeKey(StreamingScraperProvider provider, Uri url)
    {
        var path = url.AbsolutePath.TrimEnd('/');
        return provider switch
        {
            StreamingScraperProvider.BbcSounds => path.ToLowerInvariant(),
            StreamingScraperProvider.BbcIplayer =>
                // Keep episode id only — slug suffixes vary.
                string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Take(3))
                    .ToLowerInvariant(),
            StreamingScraperProvider.AmazonPrime =>
                string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Take(2))
                    .ToLowerInvariant(),
            StreamingScraperProvider.Netflix => path.ToLowerInvariant(),
            StreamingScraperProvider.Vimeo => path.ToLowerInvariant(),
            _ => url.GetLeftPart(UriPartial.Path).ToLowerInvariant()
        };
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        client.Timeout = TimeSpan.FromSeconds(45);
        return client;
    }

    [GeneratedRegex("href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HrefRegex();
}
