﻿using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Twitter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTwitterServices(this IServiceCollection services)
    {
        services.BindConfiguration<TwitterOptions>("twitter");

        return services
            .AddScoped<ITweeter, Tweeter>()
            .AddScoped<ITwitterClient, TwitterClient>()
            .AddScoped<ITweetBuilder, TweetBuilder>()
            .AddScoped<ITweetPoster, TweetPoster>()
            .AddScoped<ITweetManager, TweetManager>();
    }
}