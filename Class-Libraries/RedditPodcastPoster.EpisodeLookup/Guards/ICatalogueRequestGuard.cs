using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Fixed per-request timeout guard for catalogue HTTP calls (Spotify and Apple). Not tuned per flow.
/// When it fires it logs a warning and returns default, so the lookup moves on to its next strategy
/// or ends in a clean miss instead of a timeout.
/// </summary>
public interface ICatalogueRequestGuard
{
    Task<T?> Run<T>(
        string platform,
        string operation,
        Func<CancellationToken, Task<T?>> request,
        CancellationToken cancellationToken);
}
