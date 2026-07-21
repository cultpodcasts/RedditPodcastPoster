using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Persistence.Factories;
using RedditPodcastPoster.Persistence.Lookups;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;
using RedditPodcastPoster.Persistence.Writers;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.KnownTerms;

namespace RedditPodcastPoster.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Cosmos repositories. Does not register episodes domain or merge orchestration â€” callers that
        /// resolve matcher, merger, or UrlSubmission enrichers must call <c>AddEpisodesDomain()</c> and
        /// <c>AddPodcastServices()</c> explicitly at the composition root.
        /// </summary>
        public IServiceCollection AddRepositories()
        {
            return services
                .AddSingleton<ICosmosDbClientFactory, CosmosDbClientFactory>()
                .AddSingleton(sp => sp.GetRequiredService<ICosmosDbClientFactory>().Create())
                .AddSingleton<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
                .AddSingleton<IPodcastRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PodcastRepository>>();
                    return new PodcastRepository(containerFactory.CreatePodcastsContainer(), logger);
                })
                .AddSingleton<ILookupRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LookupRepository>>();
                    return new LookupRepository(containerFactory.CreateLookUpsContainer(), logger);
                })
                .AddSingleton<IEpisodeRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var lookupRepository = s.GetRequiredService<ILookupRepository>();
                    var podcastRepository = s.GetRequiredService<IPodcastRepository>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EpisodeRepository>>();
                    return new EpisodeRepository(
                        containerFactory.CreateEpisodesContainer(),
                        lookupRepository,
                        podcastRepository,
                        logger);
                })
                .AddSingleton<IActivityRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ActivityRepository>>();
                    return new ActivityRepository(containerFactory.CreateActivitiesContainer(), logger);
                })
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddSingleton<IEliminationTermsRepository, EliminationTermsRepository>()
                .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
                .AddSingleton<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
                .AddSingleton<IAsyncInstance<IKnownTermsProvider>>(s =>
                    new AsyncInstance<IKnownTermsProvider>(s.GetRequiredService<IKnownTermsProviderFactory>()))
                .AddSingleton<IYouTubeQuotaUsageStateStore, YouTubeQuotaUsageStateStore>()
                .AddSingleton<IYouTubeIndexerKeyStateStore, YouTubeIndexerKeyStateStore>()
                .BindConfiguration<CosmosDbSettings>("cosmosdb");
        }

        public IServiceCollection AddEliminationTerms()
        {
            return services
                .AddSingleton<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
                .AddSingleton<IAsyncInstance<IEliminationTermsProvider>>(s =>
                    new AsyncInstance<IEliminationTermsProvider>(s.GetRequiredService<IEliminationTermsProviderFactory>()));
        }

        public IServiceCollection AddFileRepository(string containerName = "",
            bool useEntityFolder = false)
        {
            return services
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
                .AddScoped(x => x.GetService<IFileRepositoryFactory>()!.Create(containerName, useEntityFolder));
        }

        public IServiceCollection AddSafeFileWriter()
        {
            return services
                .AddScoped<ISafeFileEntityWriter, SafeFileEntityWriter>();
        }
    }
}
