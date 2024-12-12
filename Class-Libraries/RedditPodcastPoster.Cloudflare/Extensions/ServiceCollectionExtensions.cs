using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Cloudflare.Factories;

namespace RedditPodcastPoster.Cloudflare.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCloudflareClients(this IServiceCollection services)
    {
        return services
            .BindConfiguration<CloudFlareOptions>("cloudflare")
            .AddScoped<IKVClient, KVClient>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>()!.Create());
    }
}