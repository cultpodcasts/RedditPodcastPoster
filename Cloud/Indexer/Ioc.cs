using Azure;
using Indexer.Categorisation;
using Indexer.Tweets;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
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
            .AddRepositories()
            .AddTextSanitiser()
            .AddYouTubeServices(hostBuilderContext.Configuration)
            .AddSpotifyServices(hostBuilderContext.Configuration)
            .AddAppleServices()
            .AddCommonServices(hostBuilderContext.Configuration)
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddEliminationTerms()
            .AddRedditServices(hostBuilderContext.Configuration)
            .AddScoped<IFlushable, CacheFlusher>()
            .AddTwitterServices(hostBuilderContext.Configuration)
            .AddScoped<ITweeter, Tweeter>()
            .AddSubjectServices()
            .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddContentPublishing(hostBuilderContext.Configuration)
            .AddHttpClient();

        serviceCollection.BindConfiguration<IndexerOptions>("indexer");
        serviceCollection.BindConfiguration<PosterOptions>("poster");
        serviceCollection.AddPostingCriteria(hostBuilderContext.Configuration);
        serviceCollection.AddDelayedYouTubePublication(hostBuilderContext.Configuration);
    }
}