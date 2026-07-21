using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Configuration;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Cloudflare.Factories;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Configuration.Extensions;

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
