namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Skips live streaming scraper tests when <see cref="StreamingScraperLiveTestGate.ShouldSkipLive"/> is true.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class LiveStreamingTheoryAttribute : TheoryAttribute
{
    public LiveStreamingTheoryAttribute()
    {
        if (StreamingScraperLiveTestGate.ShouldSkipLive)
        {
            Skip = StreamingScraperLiveTestGate.SkipReason;
        }
    }
}
