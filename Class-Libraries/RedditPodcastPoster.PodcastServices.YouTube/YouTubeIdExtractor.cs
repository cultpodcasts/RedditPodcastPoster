﻿using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public partial class YouTubeIdExtractor(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeIdExtractor> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeIdExtractor
{
    private static readonly Regex VideoId = GenerateYouTubeIdRegex();
    private static readonly Regex ShortId = GenerateShortId();
    private static readonly Regex LiveId = GenerateLiveId();

    public string? Extract(Uri youTubeUrl)
    {
        var videoIdMatch = VideoId.Match(youTubeUrl.ToString()).Groups["videoId"];
        if (videoIdMatch.Success)
        {
            return videoIdMatch.Value;
        }

        videoIdMatch = ShortId.Match(youTubeUrl.ToString()).Groups["videoId"];
        if (videoIdMatch.Success)
        {
            return videoIdMatch.Value;
        }

        videoIdMatch = LiveId.Match(youTubeUrl.ToString()).Groups["videoId"];
        if (videoIdMatch.Success)
        {
            return videoIdMatch.Value;
        }

        return null;
    }

    [GeneratedRegex(@"v=(?'videoId'[\-\w]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateYouTubeIdRegex();

    [GeneratedRegex(@"shorts/(?'videoId'[\-\w]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateShortId();

    [GeneratedRegex(@"live/(?'videoId'[\-\w]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateLiveId();
}