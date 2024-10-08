﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.CloudflareRedirect.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedirectServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.BindConfiguration<CloudFlareOptions>("cloudflare");
        services.BindConfiguration<RedirectOptions>("redirects");
        return services.AddScoped<IRedirectService, RedirectService>();
    }
}