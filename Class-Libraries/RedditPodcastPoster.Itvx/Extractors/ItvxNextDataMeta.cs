using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;

namespace RedditPodcastPoster.Itvx.Extractors;

/// <summary>
/// Reads episode art / duration / release from ITVX Pages Router <c>__NEXT_DATA__</c>.
/// Open Graph on watch pages often exposes only the ITVX brand logo and omits length/date.
/// </summary>
internal static partial class ItvxNextDataMeta
{
    public sealed record Parsed(
        string? Title,
        string? Description,
        TimeSpan? Duration,
        DateTime? Release,
        Uri? Image,
        string? ShowName);

    public static Parsed? TryParse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var script = document.DocumentNode.SelectSingleNode(
            "//script[@id='__NEXT_DATA__']");
        if (script is null || string.IsNullOrWhiteSpace(script.InnerText))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(script.InnerText);
            if (!json.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps))
            {
                return null;
            }

            pageProps.TryGetProperty("episode", out var episode);
            pageProps.TryGetProperty("programme", out var programme);

            if (episode.ValueKind != JsonValueKind.Object &&
                programme.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var title = ReadString(episode, "episodeTitle")
                        ?? ReadString(episode, "headerTitle");
            var description = ReadString(episode, "longDescription")
                              ?? ReadString(episode, "description")
                              ?? ReadString(programme, "longDescription")
                              ?? ReadString(programme, "description");
            var showName = ReadString(programme, "title")
                           ?? ReadString(episode, "programmeTitle")
                           ?? ReadString(episode, "showTitle");
            var duration = ReadDuration(episode);
            var release = ReadRelease(episode);
            var image = ReadBestImage(episode, programme);

            if (title is null &&
                description is null &&
                duration is null &&
                release is null &&
                image is null &&
                showName is null)
            {
                return null;
            }

            return new Parsed(title, description, duration, release, image, showName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static TimeSpan? ReadDuration(JsonElement episode)
    {
        if (episode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (episode.TryGetProperty("notFormattedDuration", out var iso) &&
            iso.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(iso.GetString()))
        {
            try
            {
                return XmlConvert.ToTimeSpan(iso.GetString()!);
            }
            catch (FormatException)
            {
            }
        }

        if (episode.TryGetProperty("duration", out var human) &&
            human.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(human.GetString()))
        {
            return ParseHumanDuration(human.GetString()!);
        }

        return null;
    }

    private static TimeSpan? ParseHumanDuration(string raw)
    {
        // e.g. "1h 16m", "45m", "2h"
        var hours = HumanHoursRegex().Match(raw);
        var minutes = HumanMinutesRegex().Match(raw);
        if (!hours.Success && !minutes.Success)
        {
            return null;
        }

        var h = hours.Success ? int.Parse(hours.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var m = minutes.Success ? int.Parse(minutes.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var span = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m);
        return span > TimeSpan.Zero ? span : null;
    }

    private static DateTime? ReadRelease(JsonElement episode)
    {
        foreach (var name in new[] { "broadcastDateTime", "dateTime", "availabilityFrom" })
        {
            if (episode.ValueKind != JsonValueKind.Object ||
                !episode.TryGetProperty(name, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                continue;
            }

            if (DateTime.TryParse(
                    value.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static Uri? ReadBestImage(JsonElement episode, JsonElement programme)
    {
        foreach (var source in new[] { episode, programme })
        {
            var fromPresets = ReadLargestImagePreset(source);
            if (fromPresets is not null && !IsBrandLogo(fromPresets))
            {
                return fromPresets;
            }
        }

        foreach (var source in new[] { episode, programme })
        {
            var template = ReadString(source, "image") ?? ReadString(source, "imageUrl");
            var resolved = ResolveImageTemplate(template);
            if (resolved is not null && !IsBrandLogo(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static Uri? ReadLargestImagePreset(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("imagePresets", out var presets) ||
            presets.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Uri? best = null;
        var bestWidth = -1;
        foreach (var breakpoint in presets.EnumerateObject())
        {
            if (breakpoint.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var density in breakpoint.Value.EnumerateObject())
            {
                if (density.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var urlText = density.Value.GetString();
                if (string.IsNullOrWhiteSpace(urlText) ||
                    !Uri.TryCreate(urlText, UriKind.Absolute, out var url) ||
                    IsBrandLogo(url))
                {
                    continue;
                }

                var width = ReadQueryInt(url, "w") ?? 0;
                if (width >= bestWidth)
                {
                    bestWidth = width;
                    best = url;
                }
            }
        }

        return best;
    }

    private static Uri? ResolveImageTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var resolved = template
            .Replace("{width}", "1920", StringComparison.Ordinal)
            .Replace("{height}", "1080", StringComparison.Ordinal)
            .Replace("{quality}", "80", StringComparison.Ordinal)
            .Replace("{blur}", "0", StringComparison.Ordinal)
            .Replace("{bg}", "false", StringComparison.Ordinal)
            .Replace("{class}", "01_Hero_DesktopCTV", StringComparison.Ordinal)
            .Replace("{aspect_ratio}", "16x9", StringComparison.Ordinal)
            .Replace("{distributionPartner}", "itv_hub", StringComparison.Ordinal)
            .Replace("{fallback}", "standard", StringComparison.Ordinal)
            .Replace("{treatment}", "standard", StringComparison.Ordinal);

        return Uri.TryCreate(resolved, UriKind.Absolute, out var url) ? url : null;
    }

    private static int? ReadQueryInt(Uri url, string name)
    {
        var query = url.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 &&
                pieces[0].Equals(name, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    public static bool IsBrandLogo(Uri image) =>
        BrandLogoPathRegex().IsMatch(image.AbsoluteUri);

    [GeneratedRegex(@"(\d+)\s*h", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HumanHoursRegex();

    [GeneratedRegex(@"(\d+)\s*m", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HumanMinutesRegex();

    [GeneratedRegex(
        @"itvx-logo|brands/itvx|itvstatic/assets/images/brands",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrandLogoPathRegex();
}
