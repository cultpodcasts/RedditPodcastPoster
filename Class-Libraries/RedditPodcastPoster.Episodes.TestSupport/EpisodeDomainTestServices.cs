using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.TestSupport;

public static class EpisodeDomainTestServices
{
    public static EpisodeMerger CreateMerger()
    {
        var provider = CreateServiceProvider();
        return new EpisodeMerger(
            provider.GetRequiredService<IEpisodePlatformMatcher>(),
            provider.GetRequiredService<IEpisodePlatformMerger>());
    }

    public static EpisodeMatcher CreateMatcher()
    {
        var provider = CreateServiceProvider();
        return new EpisodeMatcher(
            NullLogger<EpisodeMatcher>.Instance,
            provider.GetRequiredService<IEpisodePlatformMatcher>());
    }

    public static IEpisodePlatformMatcher CreatePlatformMatcher()
    {
        var provider = CreateServiceProvider();
        return provider.GetRequiredService<IEpisodePlatformMatcher>();
    }

    public static IPlatformEnrichmentApplicator CreateEnrichmentApplicator()
    {
        var provider = CreateServiceProvider();
        return provider.GetRequiredService<IPlatformEnrichmentApplicator>();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEpisodesDomain();
        return services.BuildServiceProvider();
    }
}
