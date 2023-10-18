using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteClient(this IServiceCollection services)
    {
        services.AddHttpClient<IRemoteClient, RemoteClient>();
        return services.AddScoped<IRemoteClient, RemoteClient>();
    }
}