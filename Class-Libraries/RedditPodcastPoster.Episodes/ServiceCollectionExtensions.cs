using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.Merging.Policies;

namespace RedditPodcastPoster.Episodes;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddEpisodesDomain()
        {
            services.AddSingleton<IEpisodePlatformApplier, EpisodePlatformApplier>();
            services.AddSingleton<IEpisodePlatformMerger, EpisodePlatformMerger>();
            services.AddSingleton<IEpisodePlatformMatcher, EpisodePlatformMatcher>();

            services.AddSingleton<IReleaseMatchStrategy, ExactReleaseMatchStrategy>();
            services.AddSingleton<IReleaseMatchStrategy, SpotifyCatalogueReleaseMatchStrategy>();
            services.AddSingleton<IReleaseMatchStrategy, YouTubePublishDelayMatchStrategy>();

            services.AddSingleton<IReleaseMergePolicy, YouTubeAuthoritativePreserveMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, YouTubeTimeBackfillMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, SpotifyNoTimeBackfillMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, AppleTimeBackfillMergePolicy>();

            return services;
        }
    }
}
