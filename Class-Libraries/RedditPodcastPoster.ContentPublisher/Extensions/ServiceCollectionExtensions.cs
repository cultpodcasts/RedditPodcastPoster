using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.ContentPublisher.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentPublishing(this IServiceCollection services)
    {
        return services
            .AddScoped<IHomepagePublisher, HomepagePublisher>()
            .AddScoped<ISubjectsPublisher, SubjectsPublisher>()
            .AddScoped<IDiscoveryPublisher, DiscoveryPublisher>()
            .AddScoped<ILanguagesPublisher, LanguagesPublisher>()
            .BindConfiguration<ContentOptions>("content")
            .AddCloudflareClients();
    }
}