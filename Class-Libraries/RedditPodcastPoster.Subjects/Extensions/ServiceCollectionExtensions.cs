using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Categorisation;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Providers;
using RedditPodcastPoster.Subjects.Repositories;
using RedditPodcastPoster.Subjects.Services;
using RedditPodcastPoster.Subjects.Warmup;
using RedditPodcastPoster.Text.Extensions;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Subject matching / categorisation graph.
        /// Self-contained for <see cref="ISubjectMatcher"/> resolution once the host has
        /// <c>AddRepositories()</c> (Cosmos container factory + title-casing rules for
        /// <see cref="RedditPodcastPoster.Text.Sanitisers.ITextSanitiser"/>).
        /// Called transitively by <c>AddSpotifyServices</c> / <c>AddAppleServices</c>.
        /// </summary>
        public IServiceCollection AddSubjectServices()
        {
            return services
                // SubjectService → ITextSanitiser (description extract for matching)
                .AddTextSanitiser()
                .AddSingleton<ISubjectRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SubjectRepository>>();
                    return new SubjectRepository(containerFactory.CreateSubjectsContainer(), logger);
                })
                .AddScoped<ISubjectService, SubjectService>()
                .AddScoped<ISubjectEnricher, SubjectEnricher>()
                .AddScoped<ISubjectMatcher, SubjectMatcher>()
                .AddSingleton<IRecycledFlareIdProvider, RecycledFlareIdProvider>()
                .AddScoped<ICategoriser, Categoriser>()
                .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
                .AddScoped<ISubjectFactory, SubjectFactory>()
                .AddScoped<IHashTagProvider, HashTagProvider>()
                .AddSingleton<ICachedSubjectProvider, CachedSubjectProvider>()
                // SubjectService needs ISubjectsProvider; hosts that only get subjects via
                // AddSpotifyServices/AddAppleServices must not omit the provider registration.
                .AddCachedSubjectProvider()
                .AddStartupWarmer<CachedSubjectsStartupWarmer>();
        }

        public IServiceCollection AddCachedSubjectProvider()
        {
            // Share the CachedSubjectProvider singleton with ICachedSubjectProvider when present
            // (AddSubjectServices); otherwise create a standalone cached provider.
            return services
                .AddSingleton<ISubjectsProvider>(s =>
                {
                    var cached = s.GetService<ICachedSubjectProvider>();
                    if (cached != null)
                    {
                        return cached;
                    }

                    return ActivatorUtilities.CreateInstance<CachedSubjectProvider>(s);
                });
        }

        public IServiceCollection AddSubjectProvider()
        {
            return services
                .AddSingleton<ISubjectsProvider>(s =>
                    (ISubjectsProvider)s.GetRequiredService<ISubjectRepository>());
        }
    }
}