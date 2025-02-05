﻿using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.BBC;

public partial class iPlayerPageMetaDataExtractor(
    ILogger<iPlayerPageMetaDataExtractor> logger
) : IiPlayerPageMetaDataExtractor
{
    private static readonly Regex DurationRegex = CreateDurationRegex();
    private static readonly Regex ReleaseRegex = CreateReleaseRegex();
    private static readonly CultureInfo Uk = new("en-GB");
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

        var titleNode = document.DocumentNode.SelectSingleNode(@"/html/head/meta[@property='og:title']");
        var descriptionNode = document.DocumentNode.SelectSingleNode(@"/html/head/meta[@property='og:description']");
        var title = titleNode?.Attributes["Content"]?.Value;
        var description = descriptionNode?.Attributes["Content"]?.Value;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            throw new NonPodcastServiceMetaDataExtractionException(url,
                $"Unable to obtain title and description. Title: '{title}', description: '{description}'.");
        }

        TimeSpan? duration = null;
        DateTime? release = null;
        var metaDataValues = document.DocumentNode.SelectNodes("//span[@class='episode-metadata__text']");
        foreach (var node in metaDataValues)
        {
            if (DurationRegex.IsMatch(node.InnerText))
            {
                var mins = DurationRegex.Match(node.InnerText).Groups["mins"].Value;
                if (!string.IsNullOrWhiteSpace(mins) && int.TryParse(mins, out var _duration))
                {
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
                    release = _dateTime;
                    if (!string.IsNullOrWhiteSpace(hour) && int.TryParse(hour, out var _hour))
                    {
                        release += TimeSpan.FromHours(_hour);
                        if (pm == "pm")
                        {
                            release += TimeSpan.FromHours(12);
                        }

                        if (!string.IsNullOrWhiteSpace(min) && int.TryParse(min, out var _min))
                        {
                            release += TimeSpan.FromMinutes(_min);
                        }
                    }
                }
            }
        }

        var imageContainer =
            document.DocumentNode.SelectNodes("//div[contains(@class, 'hero-image__picture')]/picture/source");
        var maxImage = imageContainer
            .Select(x => x.Attributes["srcset"].Value.Split(" "))
            .Select(x => new
                {Width = int.Parse(NumericPrefix.Match(x[1]).Groups["numericprefix"].Value), Url = new Uri(x[0])})
            .OrderByDescending(x => x.Width)
            .FirstOrDefault();
        var maxImageUrl = maxImage?.Url;

        var @explicit = false;
        var guidanceContainer =
            document.DocumentNode.SelectSingleNode("//div[contains(@class, 'guidance-banner')]");
        if (guidanceContainer != null)
        {
            var guidanceItems = guidanceContainer.SelectNodes("//div[@class='banner__message__inner']/span");
            @explicit = guidanceItems.Any();
        }

        return new NonPodcastServiceItemMetaData(title, description, duration, release, maxImageUrl, @explicit);
    }

    [GeneratedRegex(@"(?<mins>\d+) mins")]
    private static partial Regex CreateDurationRegex();

    [GeneratedRegex(@"((?<hour>\d+)(\:(?<min>\d+))?(?<pm>am|pm) )?(?<date>\d+ \w+ \d+)")]
    private static partial Regex CreateReleaseRegex();
    [GeneratedRegex(@"^(?<numericprefix>\d+)")]
    private static partial Regex CreateNumericPrefixRegex();
}