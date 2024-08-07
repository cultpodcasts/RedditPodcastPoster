using Azure;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects.Extensions;

namespace Discovery;

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
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddDiscovery(hostBuilderContext.Configuration)
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped(s => new iTunesSearchManager())
            .AddHttpClient();

        serviceCollection.BindConfiguration<DiscoverOptions>("discover");
    }
}