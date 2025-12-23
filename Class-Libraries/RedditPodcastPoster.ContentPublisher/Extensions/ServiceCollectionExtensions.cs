using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.ContentPublisher.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentPublishing(this IServiceCollection services)
    {
        return services
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .BindConfiguration<ContentOptions>("content")
            .AddCloudflareClients();
    }
}