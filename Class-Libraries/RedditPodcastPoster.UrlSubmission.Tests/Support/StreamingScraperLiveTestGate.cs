namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

internal static class StreamingScraperLiveTestGate
{
    private const string SkipEnvironmentVariable = "SKIP_LIVE_STREAMING_SCRAPER_TESTS";

    public static bool ShouldSkipLive =>
        string.Equals(
            Environment.GetEnvironmentVariable(SkipEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    public static string SkipReason =>
        $"Live streaming scraper tests skipped because {SkipEnvironmentVariable}=1.";
}
