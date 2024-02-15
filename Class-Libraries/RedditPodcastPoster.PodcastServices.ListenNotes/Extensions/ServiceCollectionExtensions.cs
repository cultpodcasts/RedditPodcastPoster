using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddListenNotesClient(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOptions<ListenNotesOptions>().Bind(config.GetSection("listenNotes"));

        return services
            .AddScoped<IClientFactory, ClientFactory>();
    }
}