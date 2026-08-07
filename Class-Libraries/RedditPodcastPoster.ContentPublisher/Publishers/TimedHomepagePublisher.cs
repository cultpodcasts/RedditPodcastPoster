using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

/// <summary>
/// Optional diagnostic wrapper around <see cref="IHomepagePublisher"/>.
/// Flip <see cref="EnableDiagnosticTiming"/> to emit <c>HomepagePublishTiming</c> App Insights warnings.
/// </summary>
public sealed class TimedHomepagePublisher(
    HomepagePublisher inner,
    ILogger<TimedHomepagePublisher> logger) : IHomepagePublisher
{
    // Flip to true to emit HomepagePublishTiming App Insights warnings (investigation only).
    public const bool EnableDiagnosticTiming = false;

    public async Task<PublishHomepageResult> PublishHomepage()
    {
        if (!EnableDiagnosticTiming)
        {
            return await inner.PublishHomepage();
        }

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
