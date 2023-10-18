using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeIdExtractor : IYouTubeIdExtractor
{
    private static readonly Regex VideoId = new(@"v=(?'videoId'[\-\w]+)", RegexOptions.Compiled);

    private readonly ILogger<YouTubeIdExtractor> _logger;

    public YouTubeIdExtractor(ILogger<YouTubeIdExtractor> logger)
    {
        _logger = logger;
    }

    public string? Extract(Uri youTubeUrl)
    {
        var videoIdMatch = VideoId.Match(youTubeUrl.ToString()).Groups["videoId"];
        if (videoIdMatch.Success)
        {
            return videoIdMatch.Value;
        }

        return null;
    }
}