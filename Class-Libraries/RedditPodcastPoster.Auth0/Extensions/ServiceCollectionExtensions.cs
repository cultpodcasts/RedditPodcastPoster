using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Auth0.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuth0Client()
        {
            return services
                .AddScoped<IAuth0Client, Auth0Client>()
                .BindConfiguration<Auth0Options>("auth0client");
        }

        public IServiceCollection AddAuth0Validation()
        {
            services
                .AddSingleton<ISigningKeysFactory, SigningKeysFactory>()
                .AddSingleton<IAuth0TokenValidator, Auth0TokenValidator>()
                .BindConfiguration<Auth0ValidationOptions>("auth0");
            return services;
        }
    }
}