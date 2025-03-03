﻿using Azure;
using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;

namespace Discovery;

public static class Ioc
{
    public static void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddRepositories()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddDiscovery(ApplicationUsage.Discover)
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped(s => new iTunesSearchManager())
            .AddPushSubscriptions()
            .AddContentPublishing()
            .AddRedditServices()
            .AddCloudflareClients()
            .AddHttpClient()
            .BindConfiguration<DiscoverOptions>("discover");
    }
}