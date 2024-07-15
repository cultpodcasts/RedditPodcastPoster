using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Auth0.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuth0Client(this IServiceCollection services)
    {
        services.AddScoped<IAuth0Client, Auth0Client>()
            .BindConfiguration<Auth0Options>("auth0client");
        return services;
    }
}