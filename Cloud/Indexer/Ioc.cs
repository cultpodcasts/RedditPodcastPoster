using Indexer.Data;
using Indexer.Publishing;
using Indexer.Tweets;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
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
            .AddAppleServices(hostBuilderContext.Configuration)
            .AddTextSanitiser()
            .AddPodcastServices(hostBuilderContext.Configuration)
            .AddEliminationTerms()
            .AddRedditServices(hostBuilderContext.Configuration)
            .AddTwitterServices(hostBuilderContext.Configuration)
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IFlushable, CacheFlusher>()
            .AddScoped<ITweeter, Tweeter>()

            // Content Publisher
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>()!.Create());


        serviceCollection.AddOptions<IndexerOptions>().Bind(hostBuilderContext.Configuration.GetSection("indexer"));
        serviceCollection
            .AddOptions<PosterOptions>().Bind(hostBuilderContext.Configuration.GetSection("poster"));
        serviceCollection
            .AddOptions<CloudFlareOptions>().Bind(hostBuilderContext.Configuration.GetSection("cloudflare"));
    }
}