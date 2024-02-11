using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public partial class YouTubeIdExtractor(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeIdExtractor> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IYouTubeIdExtractor
{
    private static readonly Regex VideoId = GenerateYouTubeIdRegex();

    public string? Extract(Uri youTubeUrl)
    {
        var videoIdMatch = VideoId.Match(youTubeUrl.ToString()).Groups["videoId"];
        if (videoIdMatch.Success)
        {
            return videoIdMatch.Value;
        }

        return null;
    }

    [GeneratedRegex(@"v=(?'videoId'[\-\w]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateYouTubeIdRegex();
}