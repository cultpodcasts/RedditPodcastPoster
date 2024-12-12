using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.CloudflareRedirect.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedirectServices(this IServiceCollection services)
    {
        services.BindConfiguration<CloudFlareOptions>("cloudflare");
        return services.AddScoped<IRedirectService, RedirectService>();
    }
}