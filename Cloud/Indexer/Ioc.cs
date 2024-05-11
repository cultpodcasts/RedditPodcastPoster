using Azure;
using Indexer.Auth;
using Indexer.Categorisation;
using Indexer.Tweets;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.AI.Extensions;
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
using RedditPodcastPoster.UrlSubmission.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Extensions;

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
            //.AddAuth0(hostBuilderContext.Configuration)
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
            .AddSubjectServices()
            .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
            .AddAIServices(hostBuilderContext.Configuration)
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddContentPublishing(hostBuilderContext.Configuration)
            .AddYouTubePushNotificationServices(hostBuilderContext.Configuration)
            .AddUrlSubmission()
            .AddHttpClient();

        serviceCollection.AddOptions<IndexerOptions>().Bind(hostBuilderContext.Configuration.GetSection("indexer"));
        serviceCollection
            .AddOptions<PosterOptions>().Bind(hostBuilderContext.Configuration.GetSection("poster"));
        serviceCollection.AddPostingCriteria(hostBuilderContext.Configuration);
        serviceCollection.AddDelayedYouTubePublication(hostBuilderContext.Configuration);
    }
}