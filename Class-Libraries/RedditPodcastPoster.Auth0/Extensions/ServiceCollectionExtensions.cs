using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.DependencyInjection;

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
                .AddSingleton<IAsyncInstance<ICollection<SecurityKey>?>>(x =>
                    new AsyncInstance<ICollection<SecurityKey>?>(x.GetService<ISigningKeysFactory>()!))
                .AddSingleton<IAuth0TokenValidator, Auth0TokenValidator>()
                .BindConfiguration<Auth0ValidationOptions>("auth0");
            return services;
        }
    }
}