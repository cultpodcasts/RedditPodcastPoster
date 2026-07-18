using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace FunctionHost.Tests;

internal static class FunctionHostTestSupport
{
    private const string CosmosTestAuthKey = "test-string";

    internal static IConfiguration CreateMinimalConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["cosmosdb:Endpoint"] = "https://localhost:8081",
            ["cosmosdb:AuthKeyOrResourceToken"] = CosmosTestAuthKey,
            ["cosmosdb:DatabaseId"] = "test-db",
            ["cosmosdb:PodcastsContainer"] = "Podcasts",
            ["cosmosdb:EpisodesContainer"] = "Episodes",
            ["cosmosdb:SubjectsContainer"] = "Subjects",
            ["cosmosdb:PeopleContainer"] = "People",
            ["cosmosdb:ActivitiesContainer"] = "Activity",
            ["cosmosdb:DiscoveryContainer"] = "Discovery",
            ["cosmosdb:LookUpsContainer"] = "LookUps",
            ["cosmosdb:PushSubscriptionsContainer"] = "PushSubscriptions",
            ["discover:SearchSince"] = "6:10:00",
            ["discover:LookbackMode"] = "Dynamic",
            ["discover:DynamicLookbackOverlap"] = "00:00:00",
            ["discover:scorer:Enabled"] = "false",
            ["listenNotes:Key"] = "test-listen-notes-key",
            ["listenNotes:RequestDelaySeconds"] = "1",
            ["taddy:ApiKey"] = "test-taddy-api-key",
            ["taddy:Userid"] = "test-taddy-user-id",
            ["youtube:Applications:0:ApiKey"] = "test-youtube-indexer-key",
            ["youtube:Applications:0:Name"] = "test-indexer",
            ["youtube:Applications:0:Usage"] = "Indexer",
            ["youtube:Applications:0:DisplayName"] = "Test Indexer",
            ["youtube:Applications:1:ApiKey"] = "test-youtube-discover-key",
            ["youtube:Applications:1:Name"] = "test-discover",
            ["youtube:Applications:1:Usage"] = "Discover",
            ["youtube:Applications:1:DisplayName"] = "Test Discover",
            ["youtube:Applications:2:ApiKey"] = "test-youtube-api-key",
            ["youtube:Applications:2:Name"] = "test-api",
            ["youtube:Applications:2:Usage"] = "Api",
            ["youtube:Applications:2:DisplayName"] = "Test Api",
            ["youtube:Applications:3:ApiKey"] = "test-youtube-bluesky-key",
            ["youtube:Applications:3:Name"] = "test-bluesky",
            ["youtube:Applications:3:Usage"] = "Bluesky",
            ["youtube:Applications:3:DisplayName"] = "Test Bluesky",
            ["content:BucketName"] = "test-bucket",
            ["content:HomepageKey"] = "homepage.json",
            ["content:PreProcessedHomepageKey"] = "preprocessed-homepage.json",
            ["content:SubjectsKey"] = "subjects.json",
            ["content:PeopleKey"] = "people",
            ["content:FlairsKey"] = "flairs.json",
            ["content:LanguagesKey"] = "languages.json",
            ["content:DiscoveryInfoKey"] = "discovery-info.json",
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
            ["memoryProbe:Enabled"] = "false",
            ["indexer:SkipShortEpisodes"] = "false",
            ["indexer:ReleasedDaysAgo"] = "3",
            ["indexer:ByPassYouTube"] = "false",
            ["poster:ReleasedDaysAgo"] = "4",
            ["poster:MaxPosts"] = "15",
            ["activities:RunIndex"] = "false",
            ["activities:RunCategoriser"] = "false",
            ["activities:RunPoster"] = "false",
            ["activities:RunPublisher"] = "false",
            ["activities:RunTweet"] = "false",
            ["activities:RunBluesky"] = "false",
            ["postingCriteria:MinimumDuration"] = "0:05:00",
            ["postingCriteria:TweetDays"] = "2",
            ["postingCriteria:RedditDays"] = "2",
            ["postingCriteria:BlueSkyDays"] = "2",
            ["postingCriteria:CategoriserDays"] = "2",
            ["delayedYouTubePublication:EvaluationThreshold"] = "1:00:00",
            ["hosting:AllowedHosts"] = "*",
            ["hosting:UserRoles:0"] = "curate",
            ["spotify:ClientId"] = "test-client-id",
            ["spotify:ClientSecret"] = "test-client-secret",
            ["reddit:AppId"] = "test-app-id",
            ["reddit:AppSecret"] = "test-app-secret",
            ["reddit:RefreshToken"] = "test-refresh-token",
            ["reddit:UserAgent"] = "test-user-agent",
            ["redditAdmin:AppId"] = "test-admin-app-id",
            ["redditAdmin:AppSecret"] = "test-admin-app-secret",
            ["redditAdmin:RefreshToken"] = "test-admin-refresh-token",
            ["twitter:ConsumerKey"] = "test-consumer-key",
            ["twitter:ConsumerSecret"] = "test-consumer-secret",
            ["twitter:AccessToken"] = "test-access-token",
            ["twitter:AccessTokenSecret"] = "test-access-token-secret",
            ["bluesky:Identifier"] = "test@example.com",
            ["bluesky:Password"] = "test-password",
            ["bluesky:ReuseSession"] = "false",
            ["bluesky:MaxFailures"] = "3",
            ["bluesky:MaxPosts"] = "5",
            ["pushSubscriptions:Subject"] = "mailto:test@example.com",
            ["pushSubscriptions:PublicKey"] = "test-public-key",
            ["pushSubscriptions:PrivateKey"] = "test-private-key",
            ["auth0:Domain"] = "test.auth0.com",
            ["auth0:Audience"] = "https://test-api",
            ["auth0:Issuer"] = "https://test.auth0.com/",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    internal static ServiceCollection CreateServiceCollection(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(CreateMinimalConfiguration());
        configure(services);
        ReplaceCosmosWithTestDoubles(services);
        ReplaceLookupRepositoryWithTestDouble(services);
        ReplaceSubjectsProviderWithTestDouble(services);
        return services;
    }

    internal static void ReplaceCosmosWithTestDoubles(IServiceCollection services)
    {
        RemoveService<CosmosClient>(services);
        RemoveService<ICosmosDbClientFactory>(services);
        RemoveService<ICosmosDbContainerFactory>(services);

        var mockContainer = new Mock<Container>();
        var mockContainerFactory = new Mock<ICosmosDbContainerFactory>();
        mockContainerFactory.Setup(x => x.CreatePodcastsContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreateEpisodesContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreateSubjectsContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreatePeopleContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreateActivitiesContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreateDiscoveryContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreateLookUpsContainer()).Returns(mockContainer.Object);
        mockContainerFactory.Setup(x => x.CreatePushSubscriptionsContainer()).Returns(mockContainer.Object);

        services.AddSingleton(mockContainerFactory.Object);
        services.AddSingleton(_ => new CosmosClient("https://localhost:8081", CosmosTestAuthKey));
    }

    internal static void ReplaceLookupRepositoryWithTestDouble(IServiceCollection services)
    {
        RemoveService<ILookupRepository>(services);

        var mockLookupRepository = new Mock<ILookupRepository>();
        mockLookupRepository.Setup(x => x.GetYouTubeIndexerKeyState()).ReturnsAsync((YouTubeIndexerKeyState?)null);
        mockLookupRepository.Setup(x => x.GetYouTubeQuotaUsageState()).ReturnsAsync((YouTubeQuotaUsageState?)null);
        mockLookupRepository.Setup(x => x.GetEliminationTerms()).ReturnsAsync((EliminationTerms?)null);
        mockLookupRepository.Setup(x => x.GetHomePageCache()).ReturnsAsync((HomePageCache?)null);
        mockLookupRepository.Setup(x => x.GetYouTubeQuotaReport()).ReturnsAsync((YouTubeQuotaReport?)null);

        services.AddSingleton(mockLookupRepository.Object);
    }

    internal static void ReplaceSubjectsProviderWithTestDouble(IServiceCollection services)
    {
        RemoveService<ISubjectsProvider>(services);
        RemoveService<ICachedSubjectProvider>(services);

        var emptySubjectsProvider = new EmptySubjectsProvider();
        services.AddSingleton(emptySubjectsProvider);
        services.AddSingleton<ISubjectsProvider>(emptySubjectsProvider);
        services.AddSingleton<ICachedSubjectProvider>(emptySubjectsProvider);
    }

    internal static async Task ValidateEntryPointAsync(
        IServiceCollection services,
        params Type[] entryPointTypes)
    {
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        foreach (var entryPointType in entryPointTypes)
        {
            HostCompositionValidator.ResolveFromScope(scope.ServiceProvider, entryPointType);
        }
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    private sealed class EmptySubjectsProvider : ISubjectsProvider, ICachedSubjectProvider
    {
        public IAsyncEnumerable<Subject> GetAll() => AsyncEnumerable.Empty<Subject>();
    }
}
