using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace RedditPodcastPoster.OpenGraph.Extractors;

/// <summary>
/// Recovers episode duration / release when Open Graph omits them
/// (SPA catalogues that only embed player/JSON-LD freights in page HTML).
/// </summary>
public static partial class CatalogPageTimingMeta
{
    public static (TimeSpan? Duration, DateTime? Release) Coalesce(
        TimeSpan? openGraphDuration,
        DateTime? openGraphRelease,
        string html)
    {
        var duration = openGraphDuration ?? TryDurationFromHtml(html);
        var release = openGraphRelease ?? TryReleaseFromHtml(html);
        return (duration, release);
    }

    public static TimeSpan? TryDurationFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var secondsObject = DurationSecondsObjectRegex().Match(html);
        if (secondsObject.Success &&
            int.TryParse(secondsObject.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var objectSeconds) &&
            objectSeconds > 0)
        {
            return TimeSpan.FromSeconds(objectSeconds);
        }

        var iso = IsoDurationInJsonRegex().Match(html);
        if (iso.Success)
        {
            var parsed = TryParseIsoDuration(iso.Groups[1].Value);
            if (parsed is { TotalSeconds: > 0 })
            {
                return parsed;
            }
        }

        var bareSeconds = DurationBareSecondsRegex().Match(html);
        if (bareSeconds.Success &&
            int.TryParse(bareSeconds.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    public static DateTime? TryReleaseFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var published = ReleaseDatePublishedOrUploadRegex().Match(html);
        if (published.Success && TryParseIsoUtc(published.Groups[1].Value, out var fromPublished))
        {
            return fromPublished;
        }

        var begin = RightsBeginRegex().Match(html);
        if (begin.Success && TryParseIsoUtc(begin.Groups[1].Value, out var fromBegin))
        {
            return fromBegin;
        }

        return null;
    }

    private static bool TryParseIsoUtc(string raw, out DateTime release) =>
        DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out release);

    private static TimeSpan? TryParseIsoDuration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return XmlConvert.ToTimeSpan(raw);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    [GeneratedRegex(
        @"duration\\*""\s*:\s*\{\s*\\*""seconds\\*""\s*:\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationSecondsObjectRegex();

    [GeneratedRegex(
        @"duration\\*""\s*:\s*\\*""(P[^""\\]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsoDurationInJsonRegex();

    [GeneratedRegex(
        @"\\*""duration\\*""\s*:\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationBareSecondsRegex();

    [GeneratedRegex(
        @"\\*""(?:datePublished|uploadDate)\\*""\s*:\s*\\*""([^\\""]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseDatePublishedOrUploadRegex();

    [GeneratedRegex(
        @"\\*""begin\\*""\s*:\s*\\*""([^\\""]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RightsBeginRegex();
}
