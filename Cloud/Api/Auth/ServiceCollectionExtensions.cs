using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration config)
    {
        var auth0Settings = config.GetSection("Auth0").Get<Auth0Settings>();

        if (auth0Settings != null)
        {
            Console.Out.WriteLine($"{nameof(AddAuth0)}: Found {nameof(Auth0Settings)}.");

            services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddBearerToken();
            services
                .AddFunctionsAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth0Settings.Authority;
                    options.Audience = auth0Settings.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = ClaimTypes.NameIdentifier
                    };

                    options.Events = new JwtBearerEvents
                    {
                        // Log any authentication failures
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<JwtBearerHandler>>();
                            logger.LogError(context.Exception, "Authentication failed.");
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddFunctionsAuthorization(
                //    options =>
                //{
                //    options.AddPolicy(Policies.Submit,
                //        policy => policy.Requirements.Add(new
                //            HasScopeRequirement("submit", auth0Settings.Authority)));
                //}
            );

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
        }

        return services;
    }
}