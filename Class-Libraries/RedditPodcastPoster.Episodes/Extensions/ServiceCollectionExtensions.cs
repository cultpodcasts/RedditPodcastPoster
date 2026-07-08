using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Strategies;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.Merging.Policies;

namespace RedditPodcastPoster.Episodes.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Episode platform matcher, merger, applier, and their strategies/policies.
        /// Required when resolving <c>IEpisodeMatcher</c>, <c>IEpisodeMerger</c> (via <c>AddPodcastServices()</c>),
        /// or UrlSubmission <c>IEpisodeEnricher</c>. Each host must call this explicitly at its composition root
        /// (alongside <c>AddRepositories()</c> when needed) — never nested inside
        /// <c>AddRepositories()</c>, <c>AddUrlSubmission()</c>, or any other extension method.
        /// </summary>
        public IServiceCollection AddEpisodesDomain()
        {
            services.AddSingleton<IEpisodePlatformApplier, EpisodePlatformApplier>();
            services.AddSingleton<IPlatformEnrichmentApplicator, PlatformEnrichmentApplicator>();
            services.AddSingleton<IEpisodePlatformMerger, EpisodePlatformMerger>();
            services.AddSingleton<IEpisodePlatformMatcher, EpisodePlatformMatcher>();

            services.AddSingleton<IReleaseMatchStrategy, ExactReleaseMatchStrategy>();
            services.AddSingleton<IReleaseMatchStrategy, SpotifyCatalogueReleaseMatchStrategy>();
            services.AddSingleton<IReleaseMatchStrategy, AppleCatalogueReleaseMatchStrategy>();
            services.AddSingleton<IReleaseMatchStrategy, YouTubePublishDelayMatchStrategy>();

            services.AddSingleton<IReleaseMergePolicy, YouTubeAuthoritativePreserveMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, YouTubeTimeBackfillMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, SpotifyNoTimeBackfillMergePolicy>();
            services.AddSingleton<IReleaseMergePolicy, AppleTimeBackfillMergePolicy>();

            services.AddSingleton<IEpisodeFromCandidateFactory, EpisodeFromCandidateFactory>();
            services.AddSingleton<IEpisodeCatalogueAdapter<SpotifyCatalogueInput>, SpotifyEpisodeAdapter>();
            services.AddSingleton<IEpisodeCatalogueAdapter<AppleCatalogueInput>, AppleEpisodeAdapter>();
            services.AddSingleton<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>, YouTubeEpisodeAdapter>();

            return services;
        }
    }
}
