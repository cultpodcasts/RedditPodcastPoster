namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Outcome of <see cref="IEpisodeRepository.DeleteMany"/>.</summary>
/// <param name="Deleted">Rows deleted by this call.</param>
/// <param name="Missing">Rows already gone (404), skipped.</param>
/// <param name="RequestCharge">Total request units charged for the deletes.</param>
/// <param name="ThrottleRetries">429 retries made after the SDK's own retries.</param>
public sealed record EpisodeBulkDeleteResult(int Deleted, int Missing, double RequestCharge, int ThrottleRetries);
