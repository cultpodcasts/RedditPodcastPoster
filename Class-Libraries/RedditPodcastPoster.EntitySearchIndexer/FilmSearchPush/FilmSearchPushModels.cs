namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <param name="Full">Push every film and delete film documents whose film no longer exists, ignoring the watermark.</param>
/// <param name="DryRun">Read and plan only: no search writes and no watermark save.</param>
/// <param name="BatchSize">Documents per index request (Azure AI Search accepts at most 1000).</param>
/// <param name="FilmId">When set, push only this film's document (delete it if the film is removed) and leave the
/// watermark untouched. Cannot be combined with <paramref name="Full"/>.</param>
public sealed record FilmSearchPushRequest(bool Full = false, bool DryRun = false, int BatchSize = 500, Guid? FilmId = null)
{
    public const int MaxBatchSize = 1000;
}

/// <param name="Succeeded">Every push and delete succeeded (always true for a dry run that read without error).</param>
/// <param name="FullPush">The run pushed every film (asked for, no watermark yet, or the index held no film documents).</param>
/// <param name="PreviousWatermark">The film <c>_ts</c> the run started from (null for none).</param>
/// <param name="NewWatermark">The watermark after the run: advanced only when the run succeeded and was not a dry run.</param>
/// <param name="FilmsRead">Films read from Cosmos.</param>
/// <param name="ToPush">Documents planned for merge-or-upload.</param>
/// <param name="ToDelete">Documents planned for delete (removed films, plus orphans on a full push).</param>
/// <param name="Failed">Documents that failed to map, push or delete.</param>
/// <param name="WatermarkSaved">The LookUps watermark was written.</param>
public sealed record FilmSearchPushResult(
    bool Succeeded,
    bool FullPush,
    bool DryRun,
    long? PreviousWatermark,
    long? NewWatermark,
    int FilmsRead,
    int ToPush,
    int ToDelete,
    int Failed,
    bool WatermarkSaved);
