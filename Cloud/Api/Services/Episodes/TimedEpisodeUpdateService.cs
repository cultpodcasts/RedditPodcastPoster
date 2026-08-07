using System.Diagnostics;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services.Episodes;

/// <summary>
/// Optional diagnostic wrapper around <see cref="EpisodeUpdateService"/>.
/// Flip <see cref="EnableDiagnosticTiming"/> to emit <c>EpisodeUpdateTiming</c> App Insights warnings.
/// </summary>
public sealed class TimedEpisodeUpdateService(
    EpisodeUpdateService inner,
    ILogger<TimedEpisodeUpdateService> logger) : IEpisodeUpdateService
{
    // Flip to true to emit EpisodeUpdateTiming App Insights warnings (investigation only).
    public const bool EnableDiagnosticTiming = false;

    public async Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        if (!EnableDiagnosticTiming)
        {
            return await inner.UpdateAsync(episodeChangeRequestWrapper, cancellationToken);
        }

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
