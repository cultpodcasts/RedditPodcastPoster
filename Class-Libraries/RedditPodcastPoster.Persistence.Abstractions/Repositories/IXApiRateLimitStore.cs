using RedditPodcastPoster.Models.Social;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Reads and saves the <see cref="XApiRateLimitState"/> LookUps singleton.</summary>
public interface IXApiRateLimitStore
{
    /// <returns>The saved state, or null when X has not been rate-limited or usage-capped yet.</returns>
    Task<XApiRateLimitState?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(XApiRateLimitState state, CancellationToken cancellationToken = default);
}
