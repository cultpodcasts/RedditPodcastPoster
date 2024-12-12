using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Cloudflare.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCloudflareClients(this IServiceCollection services)
    {
        return services
            .BindConfiguration<CloudFlareOptions>("cloudflare")
            .AddScoped<IKVClient, KVClient>();
    }
}