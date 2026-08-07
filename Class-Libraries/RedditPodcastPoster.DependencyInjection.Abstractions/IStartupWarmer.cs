namespace RedditPodcastPoster.DependencyInjection;

/// <summary>
/// Eager host-start warm-up for lazily initialised providers (Cosmos lookups, OIDC keys, tokens, etc.).
/// Collected by <see cref="ParallelStartupWarmupHostedService"/> and run in parallel.
/// </summary>
public interface IStartupWarmer
{
    string Name { get; }

    Task WarmAsync(CancellationToken cancellationToken);
}
