using RedditPodcastPoster.EpisodeLookup.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The episode lookup clearing-house (planner, executor, request guard, observed show sizes, options).
        /// Call at each composition root next to <c>AddEpisodesDomain()</c>. See <c>RedditPodcastPoster.EpisodeLookup/README.md</c>.
        /// </summary>
        public IServiceCollection AddEpisodeLookup()
        {
            services.TryAddSingleton<IEpisodeLookupPlanner, EpisodeLookupPlanner>();
            services.TryAddSingleton<ICatalogueRequestGuard, CatalogueRequestGuard>();
            // Scoped: the subject classifier (RedditPodcastPoster.Subjects), per-scope observed show counts,
            // and Index miss memory applied onto Podcast.LookupMisses at the end of the pass.
            services.TryAddScoped<ICatalogueShowEpisodeCounts, CatalogueShowEpisodeCounts>();
            services.TryAddScoped<ICatalogueLookupMisses, CatalogueLookupMisses>();
            services.TryAddScoped<ICatalogueLookupExecutor, CatalogueLookupExecutor>();
            services.TryAddScoped<ICatalogueSubjectClassifier, NoCatalogueSubjectClassifier>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddOptions<EpisodeLookupOptions>()
                .Configure<IServiceProvider>((options, provider) =>
                    provider.GetService<IConfiguration>()?
                        .GetSection(EpisodeLookupOptions.SectionName)
                        .Bind(options))
                .Validate(EpisodeLookupOptions.IsValid, EpisodeLookupOptions.ValidationMessage)
                .ValidateOnStart();
            return services;
        }
    }
}
