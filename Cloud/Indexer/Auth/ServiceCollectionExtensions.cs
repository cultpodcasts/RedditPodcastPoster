﻿using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Indexer.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration config)
    {
        var auth0Settings = config.GetSection("Auth0").Get<Auth0Settings>();

        if (auth0Settings != null)
        {
            Console.Out.WriteLine($"{nameof(AddAuth0)}: Found {nameof(Auth0Settings)}.");
            services
                .AddFunctionsAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer("FunctionBearer", options =>
                {
                    options.Authority = auth0Settings.Authority;
                    options.Audience = auth0Settings.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });

            services.AddFunctionsAuthorization(options =>
            {
                options.AddPolicy(Policies.Submit,
                    policy => policy.Requirements.Add(new
                        HasScopeRequirement("submit", auth0Settings.Authority)));
            });

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
        }

        return services;
    }
}