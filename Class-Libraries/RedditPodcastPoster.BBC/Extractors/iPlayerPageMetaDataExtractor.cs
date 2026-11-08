using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RedditPodcastPoster.BBC.DTOs;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BBC.Extractors;

public partial class iPlayerPageMetaDataExtractor : IiPlayerPageMetaDataExtractor
{
    private static readonly Regex DurationRegex = CreateDurationRegex();
    private static readonly Regex ReleaseRegex = CreateReleaseRegex();
    private static readonly CultureInfo Uk = new("en-GB");
    private static readonly TimeZoneInfo UkTimeZone = ResolveUkTimeZone();
    private static readonly Regex NumericPrefix = CreateNumericPrefixRegex();

    public async Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());
        var pageTypeNode = document.DocumentNode.SelectSingleNode("/html/head/meta[@property='og:type']");
        var pageType = pageTypeNode?.Attributes["Content"]?.Value;

        if (pageType != "video.episode")
        {
            throw new NonPodcastServiceMetaDataExtractionException(url,
                "Page does not have meta-tag <meta property=\"og:type\" content=\"video.episode\"/>");
        }

        var md = GetMetaData(document);
        var title = md.Title;
        var description = md.Description;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url,
                $"Unable to obtain title and description. Title: '{title}', description: '{description}'.");
        }

        if (md.Release == null)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, $"Unable to obtain release'.");
        }

        if (md.Duration == null)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, $"Unable to obtain duration'.");
        }


        // Publisher is always BBC. IMDb / TheTVDB are not present on iPlayer pages or in
        // __IPLAYER_REDUX_STATE__, so catalogue create must not invent those identity URLs.
        return new NonPodcastServiceItemMetaData(title, description, md.Duration, md.Release, md.Image,
            md.Explicit, "BBC", md.Series);
    }

    private static TextMetaData GetMetaData(HtmlDocument document)
    {
        string? description;

        var titleNode = document.DocumentNode.SelectSingleNode(@"/html/head/meta[@property='og:title']");
        var ogTitle = titleNode?.Attributes["Content"]?.Value;
        var seriesNode = document.DocumentNode.SelectSingleNode(@"/html/head/meta[@property='og:video:series']");
        var ogSeries = seriesNode?.Attributes["Content"]?.Value;
        string? reduxBrand = null;
        string? reduxSubtitle = null;

        var tvAppClientConfigScriptNode =
            document.DocumentNode.SelectSingleNode("//script[@id='tvip-script-app-store']");
        if (tvAppClientConfigScriptNode != null)
        {
            var script = tvAppClientConfigScriptNode.InnerText;
            const string marker = "__IPLAYER_REDUX_STATE__";
            var start = script.IndexOf(marker) + marker.Length;
            var json = script.Substring(start, script.LastIndexOf(";") - start).Trim().TrimStart('=').TrimStart();

            var metaData = JsonSerializer.Deserialize<BBCiPlayerMetaData>(json)
                ?? throw new InvalidOperationException("Unable to deserialize BBC iPlayer redux state.");

            description = metaData.Episode.Synopses.Description;
            // Live iPlayer: episode.title is the programme/brand; episode.subtitle is the episode label.
            reduxBrand = metaData.Episode.Title;
            reduxSubtitle = metaData.Episode.Subtitle;
        }
        else
        {
            var descriptionNode =
                document.DocumentNode.SelectSingleNode(@"/html/head/meta[@property='og:description']");
            description = descriptionNode?.Attributes["Content"]?.Value;
        }

        TimeSpan? duration = null;
        DateTime? release = null;
        var metaDataValues = document.DocumentNode.SelectNodes("//span[@class='episode-metadata__text']");
        if (metaDataValues != null)
        {
            foreach (var node in metaDataValues)
            {
                if (DurationRegex.IsMatch(node.InnerText))
                {
                    var match = DurationRegex.Match(node.InnerText);
                    var mins = match.Groups["mins"].Value;
                    if (mins == string.Empty) mins = "0";
                    if (!string.IsNullOrWhiteSpace(mins) && int.TryParse(mins, out var _duration))
                    {
                        var hours = match.Groups["hours"]?.Value;
                        if (!string.IsNullOrWhiteSpace(hours) && int.TryParse(hours, out var _hours))
                        {
                            _duration += _hours * 60;
                        }

                        duration = TimeSpan.FromMinutes(_duration);
                    }
                }
                else if (ReleaseRegex.IsMatch(node.InnerText))
                {
                    var matches = ReleaseRegex.Match(node.InnerText);
                    var hour = matches.Groups["hour"]?.Value;
                    var min = matches.Groups["min"]?.Value;
                    var pm = matches.Groups["pm"]?.Value;
                    var date = matches.Groups["date"]?.Value;
                    if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, Uk, out var _dateTime))
                    {
                        // Date only (no time on the page): deliberately left as midnight with an unspecified kind,
                        // which the UTC converters write as 00:00Z. It is a calendar date, not a UK instant, so it is
                        // not shifted by the BST offset.
                        release = _dateTime;
                        if (!string.IsNullOrWhiteSpace(hour) && int.TryParse(hour, out var _hour))
                        {
                            release += TimeSpan.FromHours(To24Hour(_hour, pm == "pm"));

                            if (!string.IsNullOrWhiteSpace(min) && int.TryParse(min, out var _min))
                            {
                                release += TimeSpan.FromMinutes(_min);
                            }

                            // iPlayer shows the broadcast time as UK wall-clock time (GMT/BST); store the UTC instant.
                            release = UkWallClockToUtc(release.Value);
                        }
                    }
                }
            }
        }

        var imageContainer =
            document.DocumentNode.SelectNodes("//div[contains(@class, 'hero-image__picture')]/picture/source");
        Uri? maxImageUrl = null;
        if (imageContainer != null)
        {
            maxImageUrl = imageContainer
                .Select(x =>
                {
                    var srcset = x.Attributes["srcset"]?.Value;
                    if (string.IsNullOrWhiteSpace(srcset))
                    {
                        return (Width: 0, Url: (Uri?)null);
                    }

                    var parts = srcset.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || !NumericPrefix.IsMatch(parts[1]))
                    {
                        return (Width: 0, Url: (Uri?)null);
                    }

                    return (
                        Width: int.Parse(NumericPrefix.Match(parts[1]).Groups["numericprefix"].Value),
                        Url: (Uri?)new Uri(parts[0]));
                })
                .Where(candidate => candidate.Url is not null)
                .OrderByDescending(candidate => candidate.Width)
                .Select(candidate => candidate.Url)
                .FirstOrDefault();
        }

        var @explicit = false;
        var guidanceContainer =
            document.DocumentNode.SelectSingleNode("//div[contains(@class, 'guidance-banner')]");
        if (guidanceContainer != null)
        {
            var guidanceItems = guidanceContainer.SelectNodes(".//div[@class='banner__message__inner']/span");
            @explicit = guidanceItems?.Any() == true;
        }

        var title = ResolveEpisodeTitle(ogTitle, reduxBrand, reduxSubtitle) ?? string.Empty;
        var series = BbcSeriesName.FromProgrammeBrand(reduxBrand, title)
                     ?? BbcSeriesName.FromDistinctCandidates(title, ogSeries)
                     ?? BrandFromOgTitle(ogTitle, title);
        return new TextMetaData(
            string.IsNullOrWhiteSpace(title) ? ogTitle : title,
            description,
            release,
            duration,
            maxImageUrl,
            @explicit,
            series);
    }

    private static string? ResolveEpisodeTitle(string? ogTitle, string? reduxBrand, string? reduxSubtitle)
    {
        if (!string.IsNullOrWhiteSpace(reduxSubtitle))
        {
            return reduxSubtitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ogTitle) &&
            !string.IsNullOrWhiteSpace(reduxBrand) &&
            ogTitle.StartsWith(reduxBrand, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = ogTitle[reduxBrand.Length..].TrimStart(' ', '-', ':');
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                return remainder;
            }
        }

        return ogTitle;
    }

    private static string? BrandFromOgTitle(string? ogTitle, string? episodeTitle)
    {
        if (string.IsNullOrWhiteSpace(ogTitle))
        {
            return null;
        }

        var separator = ogTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        return BbcSeriesName.FromProgrammeBrand(ogTitle[..separator], episodeTitle ?? string.Empty);
    }

    [GeneratedRegex(@"^(?<hourstext>(?<hours>\d+)h ?)?((?<mins>\d+)( mins|m))?$")]
    private static partial Regex CreateDurationRegex();

    [GeneratedRegex(@"((?<hour>\d+)(\:(?<min>\d+))?(?<pm>am|pm) )?(?<date>\d+ \w+ \d+)")]
    private static partial Regex CreateReleaseRegex();

    [GeneratedRegex(@"^(?<numericprefix>\d+)")]
    private static partial Regex CreateNumericPrefixRegex();


    private record TextMetaData(
        string? Title,
        string? Description,
        DateTime? Release,
        TimeSpan? Duration,
        Uri? Image,
        bool Explicit,
        string? Series);

    /// <summary>Converts a 12-hour clock hour (1-12) with am/pm to 0-23: 12am is 00, 12pm is 12.</summary>
    internal static int To24Hour(int hour12, bool pm) => hour12 % 12 + (pm ? 12 : 0);

    /// <summary>
    /// Converts a UK wall-clock time (no zone) to UTC: one hour earlier during BST, unchanged during GMT.
    /// <list type="bullet">
    /// <item>Spring-forward gap (01:00-01:59 on the last Sunday of March): the time does not exist; it is read as GMT.</item>
    /// <item>Fall-back repeated hour (01:00-01:59 on the last Sunday of October): the time happens twice; the
    /// <b>second</b> occurrence (GMT) is chosen deliberately, the same choice <c>DiscoverySchedule</c> makes for slots.</item>
    /// </list>
    /// </summary>
    internal static DateTime UkWallClockToUtc(DateTime ukWallClock)
    {
        var unspecified = DateTime.SpecifyKind(ukWallClock, DateTimeKind.Unspecified);
        if (UkTimeZone.IsInvalidTime(unspecified))
        {
            return DateTime.SpecifyKind(unspecified - UkTimeZone.BaseUtcOffset, DateTimeKind.Utc);
        }

        if (UkTimeZone.IsAmbiguousTime(unspecified))
        {
            var gmt = UkTimeZone.GetAmbiguousTimeOffsets(unspecified).Min();
            return DateTime.SpecifyKind(unspecified - gmt, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, UkTimeZone);
    }

    private static TimeZoneInfo ResolveUkTimeZone()
    {
        foreach (var id in new[] { "Europe/London", "GMT Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Unable to resolve UK time zone (tried Europe/London / GMT Standard Time).");
    }
}
