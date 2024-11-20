using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedditServices(this IServiceCollection services)
    {
        services.BindConfiguration<RedditSettings>("reddit");
        services.AddSubredditSettings();

        RedditClientFactory.AddRedditClient(services);

        return services.AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
            .AddScoped<IPostManager, PostManager>()
            .AddScoped<IPostResolver, PostResolver>()
            .AddScoped<IFlareManager, FlareManager>();
    }

    public static IServiceCollection AddSubredditSettings(this IServiceCollection services)
    {
        services.BindConfiguration<SubredditSettings>("subreddit");
        return services;
    }
}