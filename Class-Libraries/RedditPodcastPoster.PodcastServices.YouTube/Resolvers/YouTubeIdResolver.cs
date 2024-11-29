using System.Text.RegularExpressions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public static partial class YouTubeIdResolver
{
    private static readonly Regex VideoId = GenerateYouTubeIdRegex();
    private static readonly Regex ShortId = GenerateShortId();
    private static readonly Regex LiveId = GenerateLiveId();
    private static readonly Regex ShortUrlId = GenerateShortUrlId();

    public static string? Extract(Uri youTubeUrl)
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

        videoIdMatch = ShortUrlId.Match(youTubeUrl.ToString()).Groups["videoId"];
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

    [GeneratedRegex(@"be/(?'videoId'[\-\w]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateShortUrlId();
}