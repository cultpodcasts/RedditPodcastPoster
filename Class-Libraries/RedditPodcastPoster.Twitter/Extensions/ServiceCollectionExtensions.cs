﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Twitter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTwitterServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<TwitterOptions>().Bind(config.GetSection("twitter"));

        return services
            .AddScoped<ITwitterClient, TwitterClient>()
            .AddScoped<ITweetBuilder, TweetBuilder>()
            .AddScoped<IHashTagProvider, HashTagProvider>()
            .AddScoped<ITweetPoster, TweetPoster>();
    }
}