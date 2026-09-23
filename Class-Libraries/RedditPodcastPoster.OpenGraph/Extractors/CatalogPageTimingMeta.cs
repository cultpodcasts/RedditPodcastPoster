using System.Globalization;
using System.Text.Json;
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

        // Peacock watch-online: schema.org JSON-LD embeds episode/movie duration
        // (UI often hides it; nested trailer VideoObjects are shorter — take longest).
        var fromJsonLd = TryLongestIsoDurationInJsonLd(html);
        if (fromJsonLd is { TotalSeconds: > 0 })
        {
            return fromJsonLd;
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

        // Generic SEO catalogues: "Runtime 1h 28m" or compact "1h 28m" (after JSON-LD).
        var labeledRuntime = LabeledRuntimeHoursMinutesRegex().Match(html);
        if (labeledRuntime.Success &&
            int.TryParse(labeledRuntime.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) &&
            int.TryParse(labeledRuntime.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) &&
            (hours > 0 || minutes > 0))
        {
            return new TimeSpan(hours, minutes, 0);
        }

        var compactRuntime = CompactRuntimeHoursMinutesRegex().Match(html);
        if (compactRuntime.Success &&
            int.TryParse(compactRuntime.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var compactHours) &&
            int.TryParse(compactRuntime.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var compactMinutes) &&
            (compactHours > 0 || compactMinutes > 0))
        {
            return new TimeSpan(compactHours, compactMinutes, 0);
        }

        var prose = RuntimeProseRegex().Match(html);
        if (prose.Success &&
            int.TryParse(prose.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var proseHours) &&
            int.TryParse(prose.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var proseMinutes) &&
            (proseHours > 0 || proseMinutes > 0))
        {
            return new TimeSpan(proseHours, proseMinutes, 0);
        }

        return null;
    }

    public static DateTime? TryReleaseFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        // Catalogue JSON-LD: datePublished / uploadDate on TVEpisode / Movie / TVSeries only
        // (nested trailer VideoObject dates must not coerce episode Release).
        var fromJsonLd = TryReleaseFromJsonLd(html);
        if (fromJsonLd is not null)
        {
            return fromJsonLd;
        }

        // Non-script freights (SPA embeds). Strip ld+json so trailer uploadDate cannot win here.
        var withoutJsonLd = JsonLdScriptRegex().Replace(html, string.Empty);
        var published = ReleaseDatePublishedOrUploadRegex().Match(withoutJsonLd);
        if (published.Success && TryParseIsoUtc(published.Groups[1].Value, out var fromPublished))
        {
            return fromPublished;
        }

        var begin = RightsBeginRegex().Match(html);
        if (begin.Success && TryParseIsoUtc(begin.Groups[1].Value, out var fromBegin))
        {
            return fromBegin;
        }

        // Generic SEO: "Release Date 2022" (year only → UTC midnight Jan 1).
        var yearLabel = ReleaseDateYearLabelRegex().Match(html);
        if (yearLabel.Success &&
            int.TryParse(yearLabel.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var year) &&
            year is >= 1900 and <= 2100)
        {
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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

    /// <summary>
    /// Longest ISO-8601 duration inside <c>application/ld+json</c> scripts so
    /// catalogue <c>TVEpisode</c>/<c>Movie</c> <c>PT56M</c> wins over trailer <c>PT116S</c>.
    /// </summary>
    private static TimeSpan? TryLongestIsoDurationInJsonLd(string html)
    {
        TimeSpan? best = null;
        foreach (Match script in JsonLdScriptRegex().Matches(html))
        {
            var json = script.Groups[1].Value;
            foreach (Match iso in IsoDurationInJsonRegex().Matches(json))
            {
                var parsed = TryParseIsoDuration(iso.Groups[1].Value);
                if (parsed is { TotalSeconds: > 0 } && (best is null || parsed > best))
                {
                    best = parsed;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Catalogue-node release only: <c>TVEpisode</c> / <c>Movie</c> / <c>TVSeries</c>
    /// <c>datePublished</c>, then that node's <c>uploadDate</c>. Nested trailer
    /// <c>VideoObject.uploadDate</c> is ignored (not an air date).
    /// </summary>
    private static DateTime? TryReleaseFromJsonLd(string html)
    {
        foreach (Match script in JsonLdScriptRegex().Matches(html))
        {
            var release = TryReleaseFromJsonLdPayload(script.Groups[1].Value);
            if (release is not null)
            {
                return release;
            }
        }

        return null;
    }

    private static DateTime? TryReleaseFromJsonLdPayload(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryReleaseFromCatalogueJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime? TryReleaseFromCatalogueJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var fromItem = TryReleaseFromCatalogueJsonElement(item);
                    if (fromItem is not null)
                    {
                        return fromItem;
                    }
                }

                return null;

            case JsonValueKind.Object:
                if (element.TryGetProperty("@graph", out var graph))
                {
                    var fromGraph = TryReleaseFromCatalogueJsonElement(graph);
                    if (fromGraph is not null)
                    {
                        return fromGraph;
                    }
                }

                if (IsCatalogueSchemaType(element))
                {
                    if (TryGetJsonLdDate(element, "datePublished", out var published))
                    {
                        return published;
                    }

                    if (TryGetJsonLdDate(element, "uploadDate", out var uploaded))
                    {
                        return uploaded;
                    }

                    // Catalogue node present but undated — do not dig into trailer VideoObjects.
                    return null;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("@graph"))
                    {
                        continue;
                    }

                    var nested = TryReleaseFromCatalogueJsonElement(property.Value);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    private static bool IsCatalogueSchemaType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => IsCatalogueSchemaTypeName(typeElement.GetString()),
            JsonValueKind.Array => typeElement.EnumerateArray()
                .Any(t => t.ValueKind == JsonValueKind.String &&
                          IsCatalogueSchemaTypeName(t.GetString())),
            _ => false
        };
    }

    private static bool IsCatalogueSchemaTypeName(string? typeName) =>
        typeName is "TVEpisode" or "Movie" or "TVSeries";

    private static bool TryGetJsonLdDate(JsonElement element, string propertyName, out DateTime release)
    {
        release = default;
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return TryParseIsoUtc(value.GetString() ?? string.Empty, out release);
    }

    [GeneratedRegex(
        @"<script\b[^>]*\btype\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex JsonLdScriptRegex();

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

    /// <summary>
    /// Arte player <c>rights.begin</c> only — a bare JSON <c>"begin"</c> elsewhere must not
    /// coerce <see cref="TryReleaseFromHtml"/> when OG omits release.
    /// </summary>
    [GeneratedRegex(
        @"\\*""rights\\*""\s*:\s*\{[\s\S]{0,400}?\\*""begin\\*""\s*:\s*\\*""([^\\""]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RightsBeginRegex();

    /// <summary>Generic SEO catalogues: <c>Runtime 1h 28m</c> or label + value in adjacent markup.</summary>
    [GeneratedRegex(
        @"(?:Runtime|Running\s+time)\s*(?:[:\-]|\s|<[^>]+>)*(\d+)\s*h\s*(\d+)\s*m",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabeledRuntimeHoursMinutesRegex();

    /// <summary>Compact <c>1h 28m</c> (genre line / badges).</summary>
    [GeneratedRegex(
        @"(?<![A-Za-z0-9])(\d+)\s*h\s*(\d+)\s*m(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompactRuntimeHoursMinutesRegex();

    /// <summary>FAQ prose: <c>1 hour and 28 mins</c>.</summary>
    [GeneratedRegex(
        @"(\d+)\s*hours?\s+and\s+(\d+)\s*mins?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeProseRegex();

    /// <summary>Generic SEO catalogues: <c>Release Date 2022</c> or label + year in adjacent markup.</summary>
    [GeneratedRegex(
        @"Release\s*Date\s*(?:[:\-]|\s|<[^>]+>)*(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseDateYearLabelRegex();
}
