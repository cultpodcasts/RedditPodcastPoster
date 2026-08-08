using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

/// <summary>
/// Optional diagnostic wrapper around <see cref="HomepagePublisher"/>.
/// Registered only when <see cref="EnableDiagnosticTiming"/> is true (see content-publishing DI).
/// </summary>
public sealed class TimedHomepagePublisher(
    HomepagePublisher inner,
    ILogger<TimedHomepagePublisher> logger) : IHomepagePublisher
{
    // Flip to true to wrap IHomepagePublisher with this decorator and emit HomepagePublishTiming.
    public const bool EnableDiagnosticTiming = false;

    public async Task<PublishHomepageResult> PublishHomepage()
    {
        var total = Stopwatch.StartNew();
        var result = await inner.PublishHomepage();
        total.Stop();

        // Stable App Insights / console search key: Message startswith "HomepagePublishTiming".
        logger.LogWarning(
            "HomepagePublishTiming total-ms='{TotalMs}' published='{Published}'.",
            total.ElapsedMilliseconds,
            result.HomepagePublished);

        return result;
    }
}
