using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;

namespace Discover;

public static class Ioc
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddEpisodesDomain()
            .AddRepositories()
            .AddDiscovery(ApplicationUsage.Cli)
            .AddContentPublishing()
            .AddScoped<DiscoveryProcessor>()
            .AddScoped<IDiscoveryResultConsoleLogger, DiscoveryResultConsoleLogger>()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped(_ => new iTunesSearchManager())
            .AddHttpClient();
    }
}
