using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.OpenGraph.Extractors;

/// <summary>
/// Shared discovery+ / HBO Max (WBD) catalogue HTML helpers: clean Watch marketing descriptions,
/// prefer episode beam stills over series OG covers, upgrade beam <c>w=</c> to a high-res size,
/// and resolve show / episode titles from the same WBD watch URL shape.
/// </summary>
public static partial class WbdCatalogPageMeta
{
    public const int PreferredBeamWidth = 2000;

    private const string BeamDeliverablesSegment = "/BEAM_LWM_DELIVERABLES/";

    /// <summary>
    /// Strips <c>Watch {show}: Season N, Episode M - {title} .</c> (and Max's <c>on HBO Max</c>) so
    /// the stored description is the synopsis blurb, not the marketing lead-in.
    /// </summary>
    public static string CleanWatchMarketingDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var description = WebUtility.HtmlDecode(raw).Trim();
        var match = WatchMarketingLeadInRegex().Match(description);
        if (match.Success)
        {
            var blurb = match.Groups[1].Value.Trim();
            return blurb;
        }

        return description;
    }

    /// <summary>
    /// Prefers a beam still whose path contains the episode UUID (not the series cover in
    /// <c>og:image</c>), at the largest explicit <c>w=</c> rewritten to <see cref="PreferredBeamWidth"/>.
    /// Falls back to upgrading the Open Graph image width.
    /// </summary>
    public static Uri? TryEpisodeImage(Uri url, string html, Uri? openGraphImage)
    {
        if (TryGetEpisodeId(url, out var episodeId))
        {
            var best = FindBestBeamStillForEpisode(html, episodeId);
            if (best is not null)
            {
                return PreferFullBeamWidth(best);
            }
        }

        return openGraphImage is null ? null : PreferFullBeamWidth(openGraphImage);
    }

    /// <summary>
    /// Prefer <c>og:image:alt</c> (clean brand), then JSON-LD TVSeries / Open Graph show name,
    /// then a parsed <c>Watch {Show} | Season…</c> title. Never keep the full episode OG title as
    /// ShowName — that created one TvShow per episode in prod (#1587).
    /// </summary>
    public static string? ResolveShowName(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph,
        string cleanedOgTitle,
        string publisher)
    {
        var fromAlt = FirstGroup(html, OgImageAltRegex());
        var fromJsonLd = openGraph?.ShowName ?? FirstGroup(html, TvSeriesNameRegex());
        var fromTitle = ParseShowNameFromWatchTitle(cleanedOgTitle);

        var showName = OpenGraphSeriesName.FromDistinctCandidates(
            cleanedOgTitle,
            publisher,
            fromAlt,
            fromJsonLd,
            fromTitle);

        if (showName is null &&
            StreamingCataloguePathHints.IsSeriesPath(url) &&
            !IsEpisodePath(url))
        {
            // Show hub only: title-as-ShowName after stripping Watch / season tails.
            showName = fromTitle ?? StripWatchPrefix(cleanedOgTitle);
        }

        if (string.Equals(showName, publisher, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return showName;
    }

    /// <summary>
    /// Episode title from the Watch marketing description, then the <c>eN-slug</c> path segment,
    /// then a season-tail / show-prefix strip of the cleaned OG title.
    /// </summary>
    public static string? ResolveEpisodeTitle(
        Uri url,
        string cleanedOgTitle,
        string? description,
        string? showName)
    {
        if (!IsEpisodePath(url))
        {
            return StripWatchPrefix(cleanedOgTitle);
        }

        var fromDescription = TryEpisodeTitleFromDescription(description);
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            return fromDescription;
        }

        var fromSlug = EpisodeTitleFromPath(url);
        if (!string.IsNullOrWhiteSpace(fromSlug))
        {
            return fromSlug;
        }

        // Last resort: strip "Watch {show} | Season N Episode M" down to a season label, not the show brand.
        var afterSeason = SeasonEpisodeTailRegex().Match(cleanedOgTitle);
        if (afterSeason.Success)
        {
            return afterSeason.Groups[1].Value.Trim();
        }

        if (!string.IsNullOrWhiteSpace(showName) &&
            cleanedOgTitle.StartsWith(showName, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = cleanedOgTitle[showName.Length..].Trim().TrimStart('|', ':', '-').Trim();
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                return remainder;
            }
        }

        return StripWatchPrefix(cleanedOgTitle);
    }

    /// <summary>
    /// Episode watch pages: show UUID then <c>sN</c> / <c>e…</c> segments
    /// (<c>/show/{uuid}/s1/e1-slug/{episodeUuid}</c>).
    /// </summary>
    public static bool IsEpisodePath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part =>
            SeasonSegmentRegex().IsMatch(part) || EpisodeSegmentRegex().IsMatch(part));
    }

    /// <summary>Last Guid segment in the path (episode id on WBD watch URLs).</summary>
    public static bool TryGetEpisodeId(Uri url, out string episodeId)
    {
        episodeId = string.Empty;
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (Guid.TryParse(parts[i], out _))
            {
                episodeId = parts[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Episode title from the Watch marketing description (<c>Season N, Episode M - {title} .</c>),
    /// else null.
    /// </summary>
    public static string? TryEpisodeTitleFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var fromDescription = DescriptionEpisodeTitleRegex().Match(WebUtility.HtmlDecode(description));
        if (!fromDescription.Success)
        {
            return null;
        }

        return fromDescription.Groups[1].Value.Trim().TrimEnd('.');
    }

    /// <summary>Show brand from <c>Watch {show} | Season…</c> OG titles.</summary>
    public static string? ParseShowNameFromWatchTitle(string cleanedOgTitle)
    {
        var withoutWatch = StripWatchPrefix(cleanedOgTitle);
        var beforeSeason = SeasonSplitRegex().Match(withoutWatch);
        if (beforeSeason.Success)
        {
            return beforeSeason.Groups[1].Value.Trim().TrimEnd(':', '|', '-').Trim();
        }

        return string.IsNullOrWhiteSpace(withoutWatch) ? null : withoutWatch;
    }

    public static string StripWatchPrefix(string title)
    {
        const string watch = "Watch ";
        if (title.StartsWith(watch, StringComparison.OrdinalIgnoreCase))
        {
            return title[watch.Length..].Trim();
        }

        return title.Trim();
    }

    /// <summary>
    /// Rewrites beam deliverable CDN <c>w=</c> to <see cref="PreferredBeamWidth"/> when present or
    /// missing; leaves non-beam / non-deliverable URLs unchanged.
    /// </summary>
    public static Uri PreferFullBeamWidth(Uri image)
    {
        if (!IsBeamHost(image.Host) || !IsBeamDeliverablePath(image.AbsolutePath))
        {
            return image;
        }

        var builder = new UriBuilder(image)
        {
            Query = UpsertQueryParameter(image.Query, "w", PreferredBeamWidth.ToString(CultureInfo.InvariantCulture))
        };
        return builder.Uri;
    }

    private static string? EpisodeTitleFromPath(Uri url)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var match = EpisodeSlugRegex().Match(part);
            if (!match.Success)
            {
                continue;
            }

            var slug = match.Groups[1].Value.Replace('-', ' ').Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug);
        }

        return null;
    }

    private static Uri? FindBestBeamStillForEpisode(string html, string episodeId)
    {
        // Asset path (no query) → largest explicit w= seen for that asset.
        var bestByAsset = new Dictionary<string, (Uri Url, int Width)>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in BeamDeliverableUrlRegex().Matches(html))
        {
            var raw = WebUtility.HtmlDecode(match.Value).Replace("&amp;", "&", StringComparison.Ordinal);
            if (!raw.Contains(episodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(raw.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? raw : $"https://{raw}",
                    UriKind.Absolute, out var uri) ||
                !IsBeamHost(uri.Host) ||
                !IsBeamDeliverablePath(uri.AbsolutePath))
            {
                continue;
            }

            var pathKey = uri.GetLeftPart(UriPartial.Path);
            var width = ReadBeamWidth(uri) ?? 0;
            if (!bestByAsset.TryGetValue(pathKey, out var existing) || width > existing.Width)
            {
                bestByAsset[pathKey] = (uri, width);
            }
        }

        if (bestByAsset.Count == 0)
        {
            return null;
        }

        return bestByAsset.Values.OrderByDescending(item => item.Width).First().Url;
    }

    private static int? ReadBeamWidth(Uri uri)
    {
        var raw = ReadQueryParameter(uri.Query, "w");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            ? width
            : null;
    }

    private static string? ReadQueryParameter(string query, string name)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = eq < 0 ? pair : pair[..eq];
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..]);
        }

        return null;
    }

    /// <summary>Returns query without leading <c>?</c>, with <paramref name="name"/> set/replaced.</summary>
    private static string UpsertQueryParameter(string query, string name, string value)
    {
        var pairs = new List<string>();
        var replaced = false;
        var trimmed = (query ?? string.Empty).TrimStart('?');
        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = eq < 0 ? pair : pair[..eq];
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    pairs.Add($"{name}={Uri.EscapeDataString(value)}");
                    replaced = true;
                }
                else
                {
                    pairs.Add(pair);
                }
            }
        }

        if (!replaced)
        {
            pairs.Add($"{name}={Uri.EscapeDataString(value)}");
        }

        return string.Join('&', pairs);
    }

    /// <summary>
    /// Hosts we rewrite for beam stills — not every <c>*.warnermediacdn.com</c> asset.
    /// </summary>
    private static bool IsBeamHost(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (h.StartsWith("www.", StringComparison.Ordinal))
        {
            h = h[4..];
        }

        return h is "beam-images.warnermediacdn.com" or "wbd-images.prod-vod.h264.io";
    }

    private static bool IsBeamDeliverablePath(string absolutePath) =>
        absolutePath.Contains(BeamDeliverablesSegment, StringComparison.OrdinalIgnoreCase);

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    /// <summary>
    /// <c>Watch …: Season N, Episode M - {title} . {blurb}</c> or Max's
    /// <c>… - {title} on HBO Max. {blurb}</c>. Uses space-dot / on-HBO-Max end so
    /// mid-title periods are not treated as the blurb separator.
    /// </summary>
    [GeneratedRegex(
        @"^Watch\s+.+:\s*Season\s+\d+\s*,\s*Episode\s+\d+\s*-\s*.+?(?:\s+on\s+HBO\s+Max\s*\.|\s+\.)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex WatchMarketingLeadInRegex();

    [GeneratedRegex(
        @"https?://(?:beam-images\.warnermediacdn\.com|wbd-images\.prod-vod\.h264\.io)/BEAM_LWM_DELIVERABLES/[^\s""'<>)\\]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BeamDeliverableUrlRegex();

    [GeneratedRegex(@"^s\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonSegmentRegex();

    [GeneratedRegex(@"^e\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSegmentRegex();

    [GeneratedRegex(
        @"Season\s+\d+\s*,\s*Episode\s+\d+\s*-\s*(.+?)(?:\s+\.|\s+on\s+HBO\s+Max\s*\.|\||$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DescriptionEpisodeTitleRegex();

    [GeneratedRegex(
        @"^(.*?)\s*\|\s*Season\s+\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonSplitRegex();

    [GeneratedRegex(
        @"\|\s*(Season\s+\d+\s+Episode\s+\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonEpisodeTailRegex();

    [GeneratedRegex(@"^e\d+-(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeSlugRegex();

    [GeneratedRegex("(?:property|name)=\"og:image:alt\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgImageAltRegex();

    [GeneratedRegex(
        "\"@type\"\\s*:\\s*\"TVSeries\"[\\s\\S]{0,400}?\"name\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex TvSeriesNameRegex();
}
