using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.ContentPublisher.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentPublishing(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<CloudFlareOptions>().Bind(config.GetSection("cloudflare"));

        return services
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>()!.Create());
    }
}