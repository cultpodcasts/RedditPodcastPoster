using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.CloudflareRedirect.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedirectServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IRedirectService, RedirectService>()
            .BindConfiguration<RedirectOptions>("redirect");
    }
}