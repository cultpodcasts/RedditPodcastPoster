using Api.Configuration;
using Api.Handlers;
using Api.Services;
using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.CloudflareRedirect.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Reddit.Extensions;
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
            .AddUrlSubmission()
            .AddDiscoveryRepository()
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
            .AddScoped<IPodcastHandler, PodcastHandler>()
            .AddScoped<IEpisodeHandler, EpisodeHandler>()
            .AddScoped<IPublicHandler, PublicHandler>()
            .AddScoped<IPublishHandler, PublishHandler>()
            .BindConfiguration<HostingOptions>("hosting")
            .BindConfiguration<IndexerOptions>("indexer")
            .AddPostingCriteria();
    }
}