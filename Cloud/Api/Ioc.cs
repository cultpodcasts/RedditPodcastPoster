using Microsoft.Extensions.DependencyInjection;
using Api.Configuration;
using Api.Extensions;
using Api.Factories;
using Api.Services.Discovery;
using Azure.Diagnostics;
using iTunesSearch.Library;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.CloudflareRedirect.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Reddit.Factories;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace Api;

public static class Ioc
{
    public static void ConfigureServices(IServiceCollection serviceCollection)
    {
        AdminRedditClientFactory.AddAdminRedditClient(serviceCollection);

        serviceCollection
            .AddEpisodesDomain()
            .AddRepositories()
            .AddTextSanitiser()
            .AddYouTubeServices(ApplicationUsage.Api)
            .AddSpotifyServices()
            .AddAppleServices()
            .AddPodcastServices()
            .AddCommonServices()
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddSubjectServices()
            .AddSubjectProvider()
            .AddPeopleServices()
            .AddUrlSubmission()
            .AddDiscoveryRepository()
            .AddSingleton<IDiscoveryResultDeduplicator, DiscoveryResultDeduplicator>()
            .AddScoped<IDiscoveryResultsService, DiscoveryResultsService>()
            .AddIndexer()
            .AddEliminationTerms()
            .AddContentPublishing()
            .AddTwitterServices()
            .AddBlueskyServices()
            .AddRedditServices()
            .AddCloudflareClients()
            .AddShortnerServices()
            .AddRedirectServices()
            .AddPushSubscriptionsRepository()
            .AddScoped<IClientPrincipalFactory, ClientPrincipalFactory>()
            .AddAuth0Validation()
            .AddBBCServices()
            .AddInternetArchiveServices()
            .AddHttpClient()
            .AddEpisodeSearchIndexerService()
            .AddApiEpisodes()
            .AddApiPodcasts()
            .AddApiPeople()
            .AddApiSubjects()
            .AddApiPublic()
            .AddApiHomepage()
            .AddApiPushSubscriptions()
            .AddApiSearchIndex()
            .AddApiSubmitUrl()
            .AddApiTerms()
            .AddApiDiscovery()
            .AddApiDiscoverySchedule()
            .BindConfiguration<HostingOptions>("hosting")
            .BindConfiguration<IndexerOptions>("indexer")
            .BindConfiguration<MemoryProbeOptions>("memoryProbe")
            .AddSingleton<IMemoryProbeOrchestrator, MemoryProbeOrchestrator>()
            .AddPostingCriteria();
    }
}
