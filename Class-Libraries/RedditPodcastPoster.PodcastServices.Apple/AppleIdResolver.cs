using System.Text.RegularExpressions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class AppleIdResolver
{
    private static readonly Regex AppleIds = new(@"podcast/[\w\-\d%]+/id(?'podcastId'\d+)\?i=(?'episodeId'\d+)");

    public static long? GetEpisodeId(Uri url)
    {
        var match = AppleIds.Match(url.ToString()).Groups["episodeId"];
        return match.Success ? long.Parse(match.Value) : null;
    }

    public static long? GetPodcastId(Uri url)
    {
        var match = AppleIds.Match(url.ToString()).Groups["podcastId"];
        return match.Success ? long.Parse(match.Value) : null;
    }
}