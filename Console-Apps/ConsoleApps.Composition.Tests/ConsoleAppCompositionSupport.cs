using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Providers;

namespace ConsoleApps.Composition.Tests;

/// <summary>
/// Builds a console app's service collection against synthetic configuration with Cosmos replaced by
/// test doubles, so composition tests resolve the app's entry point without touching any cloud service.
/// Coverage gap: ILookupRepository, ILanguageTitleCasingRulesRepository, ISubjectsProvider and ICachedSubjectProvider
/// are also replaced with doubles (their real implementations read Cosmos at construction), so these tests do not
/// cover the dependency graphs of those real implementations.
/// </summary>
internal static class ConsoleAppCompositionSupport
{
    private const string CosmosTestAuthKey = "dGVzdC1jb3Ntb3Mta2V5";

    internal static ServiceCollection CreateServiceCollection(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(CreateSyntheticConfiguration());
        configure(services);
        var mocker = new AutoMocker();
        ReplaceCosmosWithTestDoubles(services, mocker);
        ReplaceRepositoriesReadAtConstructionWithTestDoubles(services, mocker);
        return services;
    }

    /// <summary>
    /// Resolves the entry point from the root provider, as each Program.cs does with host.Services, using the
    /// non-Development host defaults (no scope validation) that the published CLIs run with.
    /// </summary>
    internal static async Task ResolveEntryPointAsync(IServiceCollection services, Type entryPointType)
    {
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = false
        });
        provider.GetRequiredService(entryPointType);
    }

    private static IConfiguration CreateSyntheticConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["cosmosdb:Endpoint"] = "https://localhost:8081",
            ["cosmosdb:AuthKeyOrResourceToken"] = CosmosTestAuthKey,
            ["cosmosdb:DatabaseId"] = "test-db",
            ["cosmosdb:PodcastsContainer"] = "Podcasts",
            ["cosmosdb:EpisodesContainer"] = "Episodes",
            ["cosmosdb:TvShowsContainer"] = "TvShows",
            ["cosmosdb:TvShowEpisodesContainer"] = "TvShowEpisodes",
            ["cosmosdb:FilmsContainer"] = "Films",
            ["cosmosdb:NewsOrganisationsContainer"] = "NewsOrganisations",
            ["cosmosdb:NewsReportsContainer"] = "NewsReports",
            ["cosmosdb:SubjectsContainer"] = "Subjects",
            ["cosmosdb:PeopleContainer"] = "People",
            ["cosmosdb:ActivitiesContainer"] = "Activity",
            ["cosmosdb:DiscoveryContainer"] = "Discovery",
            ["cosmosdb:DiscoverySubmissionsContainer"] = "DiscoverySubmissions",
            ["cosmosdb:LookUpsContainer"] = "LookUps",
            ["cosmosdb:TitleCasingRulesContainer"] = "TitleCasingRules",
            ["cosmosdb:PushSubscriptionsContainer"] = "PushSubscriptions",
            ["youtube:Applications:0:ApiKey"] = "test-youtube-cli-key",
            ["youtube:Applications:0:Name"] = "test-cli",
            ["youtube:Applications:0:Usage"] = "Cli",
            ["youtube:Applications:0:DisplayName"] = "Test Cli",
            ["content:BucketName"] = "test-bucket",
            ["content:HomepageKey"] = "homepage.json",
            ["content:PreProcessedHomepageKey"] = "preprocessed-homepage.json",
            ["content:SubjectsKey"] = "subjects.json",
            ["content:PeopleKey"] = "people",
            ["content:FlairsKey"] = "flairs.json",
            ["content:LanguagesKey"] = "languages.json",
            ["content:DiscoveryInfoKey"] = "discovery-info.json",
            ["content:SearchSuggestionsKey"] = "search-suggestions.json",
            ["cloudflare:AccountId"] = "test-account-id",
            ["cloudflare:R2AccessKey"] = "test-r2-access-key",
            ["cloudflare:R2SecretKey"] = "test-r2-secret-key",
            ["cloudflare:KVApiToken"] = "test-kv-api-token",
            ["shortner:ShortnerUrl"] = "https://short.test/",
            ["shortner:KVShortnerNamespaceId"] = "test-shortner-namespace",
            ["redirect:KVRedirectNamespaceId"] = "test-redirect-namespace",
            ["searchIndex:Url"] = "https://test.search.windows.net",
            ["searchIndex:IndexName"] = "test-index",
            ["searchIndex:Key"] = "test-search-key",
            ["searchIndex:IndexerName"] = "test-indexer",
            ["indexer:SkipShortEpisodes"] = "false",
            ["indexer:ReleasedDaysAgo"] = "3",
            ["indexer:ByPassYouTube"] = "false",
            ["poster:ReleasedDaysAgo"] = "4",
            ["poster:MaxPosts"] = "15",
            ["postingCriteria:MinimumDuration"] = "0:05:00",
            ["postingCriteria:TweetDays"] = "2",
            ["postingCriteria:RedditDays"] = "2",
            ["postingCriteria:BlueSkyDays"] = "2",
            ["postingCriteria:CategoriserDays"] = "2",
            ["delayedYouTubePublication:EvaluationThreshold"] = "1:00:00",
            ["spotify:ClientId"] = "test-client-id",
            ["spotify:ClientSecret"] = "test-client-secret",
            ["reddit:AppId"] = "test-app-id",
            ["reddit:AppSecret"] = "test-app-secret",
            ["reddit:RefreshToken"] = "test-refresh-token",
            ["reddit:UserAgent"] = "test-user-agent",
            ["twitter:ConsumerKey"] = "test-consumer-key",
            ["twitter:ConsumerSecret"] = "test-consumer-secret",
            ["twitter:AccessToken"] = "test-access-token",
            ["twitter:AccessTokenSecret"] = "test-access-token-secret",
            ["bluesky:Identifier"] = "test@example.com",
            ["bluesky:Password"] = "test-password",
            ["bluesky:ReuseSession"] = "false",
            ["bluesky:MaxFailures"] = "3",
            ["bluesky:MaxPosts"] = "5",
            ["api:Endpoint"] = "https://api.test.example/",
            ["auth0client:Domain"] = "test.auth0.com",
            ["auth0client:Audience"] = "https://test-api",
            ["auth0client:ClientId"] = "test-m2m-client-id",
            ["auth0client:ClientSecret"] = "test-m2m-client-secret",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void ReplaceCosmosWithTestDoubles(IServiceCollection services, AutoMocker mocker)
    {
        RemoveService<CosmosClient>(services);
        RemoveService<ICosmosDbClientFactory>(services);
        RemoveService<ICosmosDbContainerFactory>(services);

        var container = mocker.Get<Container>();
        var containerFactory = mocker.GetMock<ICosmosDbContainerFactory>();
        containerFactory.Setup(x => x.CreatePodcastsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateEpisodesContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateTvShowsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateTvShowEpisodesContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateFilmsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateNewsOrganisationsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateNewsReportsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateSubjectsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreatePeopleContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateActivitiesContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateDiscoveryContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateDiscoverySubmissionsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateLookUpsContainer()).Returns(container);
        containerFactory.Setup(x => x.CreateTitleCasingRulesContainer()).Returns(container);
        containerFactory.Setup(x => x.CreatePushSubscriptionsContainer()).Returns(container);

        services.AddSingleton(containerFactory.Object);
        services.AddSingleton(_ => new CosmosClient("https://localhost:8081", CosmosTestAuthKey));
    }

    private static void ReplaceRepositoriesReadAtConstructionWithTestDoubles(IServiceCollection services, AutoMocker mocker)
    {
        RemoveService<ILookupRepository>(services);
        services.AddSingleton(mocker.Get<ILookupRepository>());

        RemoveService<ILanguageTitleCasingRulesRepository>(services);
        mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(x => x.GetAll()).Returns(AsyncEnumerable.Empty<TitleCasingRulesDocument>());
        services.AddSingleton(mocker.Get<ILanguageTitleCasingRulesRepository>());

        RemoveService<ISubjectsProvider>(services);
        RemoveService<ICachedSubjectProvider>(services);
        var subjects = new EmptySubjectsProvider();
        services.AddSingleton<ISubjectsProvider>(subjects);
        services.AddSingleton<ICachedSubjectProvider>(subjects);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(descriptor);
        }
    }

    private sealed class EmptySubjectsProvider : ISubjectsProvider, ICachedSubjectProvider
    {
        public IAsyncEnumerable<Subject> GetAll() => AsyncEnumerable.Empty<Subject>();
    }
}
