﻿using Azure;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Search.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;

namespace Indexer;

public static class Ioc
{
    public static void ConfigureServices(
        HostBuilderContext hostBuilderContext,
        IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddRepositories()
            .AddTextSanitiser()
            .AddYouTubeServices(ApplicationUsage.Indexer)
            .AddSpotifyServices()
            .AddAppleServices()
            .AddCommonServices()
            .AddPodcastServices()
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddEliminationTerms()
            .AddRedditServices()
            .AddScoped<IFlushable, CacheFlusher>()
            .AddTwitterServices()
            .AddBlueskyServices()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddContentPublishing()
            .AddShortnerServices()
            .AddDateTimeService()
            .AddScoped<IIndexingStrategy, IndexingStrategy>()
            .AddSearch()
            .AddHttpClient();

        serviceCollection.BindConfiguration<IndexerOptions>("indexer");
        serviceCollection.BindConfiguration<PosterOptions>("poster");
        serviceCollection.AddPostingCriteria();
        serviceCollection.AddDelayedYouTubePublication();
    }
}