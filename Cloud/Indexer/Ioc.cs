﻿using Indexer.Categorisation;
using Indexer.Data;
using Indexer.Publishing;
using Indexer.Tweets;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.AI.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;

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
            .AddRepositories(hostBuilderContext.Configuration)
            .AddTextSanitiser()
            .AddYouTubeServices(hostBuilderContext.Configuration)
            .AddSpotifyServices(hostBuilderContext.Configuration)
            .AddAppleServices()
            .AddPodcastServices(hostBuilderContext.Configuration)
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddEliminationTerms()
            .AddRedditServices(hostBuilderContext.Configuration)
            .AddScoped<IFlushable, CacheFlusher>()
            .AddTwitterServices(hostBuilderContext.Configuration)
            .AddScoped<ITweeter, Tweeter>()
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>()!.Create())
            .AddSubjectServices()
            .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
            .AddAIServices(hostBuilderContext.Configuration)
            .AddHttpClient();


        serviceCollection.AddOptions<IndexerOptions>().Bind(hostBuilderContext.Configuration.GetSection("indexer"));
        serviceCollection
            .AddOptions<PosterOptions>().Bind(hostBuilderContext.Configuration.GetSection("poster"));
        serviceCollection
            .AddOptions<CloudFlareOptions>().Bind(hostBuilderContext.Configuration.GetSection("cloudflare"));
    }
}