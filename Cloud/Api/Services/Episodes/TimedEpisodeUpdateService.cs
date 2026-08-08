using System.Diagnostics;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services.Episodes;

/// <summary>
/// Optional diagnostic wrapper around <see cref="EpisodeUpdateService"/>.
/// Registered only when <see cref="EnableDiagnosticTiming"/> is true (see Api episode DI).
/// </summary>
public sealed class TimedEpisodeUpdateService(
    EpisodeUpdateService inner,
    ILogger<TimedEpisodeUpdateService> logger) : IEpisodeUpdateService
{
    // Flip to true to wrap IEpisodeUpdateService with this decorator and emit EpisodeUpdateTiming.
    public const bool EnableDiagnosticTiming = false;

    public async Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        EpisodeUpdateResult result;
        try
        {
            result = await inner.UpdateAsync(episodeChangeRequestWrapper, cancellationToken);
        }
        catch
        {
            total.Stop();
            logger.LogWarning(
                "EpisodeUpdateTiming episode-id='{EpisodeId}' status='{Status}' total-ms='{TotalMs}'.",
                episodeChangeRequestWrapper.EpisodeId,
                EpisodeUpdateStatus.Failed,
                total.ElapsedMilliseconds);
            throw;
        }

        total.Stop();
        // Stable App Insights search key: Message startswith "EpisodeUpdateTiming".
        logger.LogWarning(
            "EpisodeUpdateTiming episode-id='{EpisodeId}' status='{Status}' total-ms='{TotalMs}'.",
            episodeChangeRequestWrapper.EpisodeId,
            result.Status,
            total.ElapsedMilliseconds);
        return result;
    }
}
