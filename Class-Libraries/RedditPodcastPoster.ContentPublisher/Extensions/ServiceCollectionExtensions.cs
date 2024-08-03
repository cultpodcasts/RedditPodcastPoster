using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Factories;

namespace RedditPodcastPoster.ContentPublisher.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentPublishing(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<CloudFlareOptions>("cloudflare");

        return services
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>()!.Create());
    }
}