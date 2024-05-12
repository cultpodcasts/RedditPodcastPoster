using AzureFunctions.Extensions.OpenIDConnect.Configuration;
using AzureFunctions.Extensions.OpenIDConnect.Isolated.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration config)
    {
        var auth0Settings = config.GetSection("Auth0").Get<Auth0Settings>();

        if (auth0Settings != null)
        {
            Console.Out.WriteLine($"{nameof(AddAuth0)}: Found {nameof(Auth0Settings)}.");
            services.AddOpenIDConnect(config =>
            {
                config.SetTokenValidation(
                    TokenValidationParametersHelpers.Default(auth0Settings.Audience, auth0Settings.Authority));
                config.SetIssuerBaseUrlConfiguration(auth0Settings.Authority);
            });
        }

        return services;
    }
}