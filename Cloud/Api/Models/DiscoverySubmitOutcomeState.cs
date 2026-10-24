namespace Api.Models;

/// <summary>
/// Wire vocabulary for POST DiscoveryCuration/submit-outcome.
/// Distinct from <see cref="RedditPodcastPoster.Models.Discovery.DiscoveryResultState"/>:
/// includes query-only <see cref="Pending"/> (found, unset stamp) and <see cref="Unknown"/> (id not in documents).
/// </summary>
public enum DiscoverySubmitOutcomeState
{
    Accepted,
    AcceptError,
    Rejected,
    Pending,
    Unknown
}
