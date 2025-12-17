using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedditServices(this IServiceCollection services)
    {
        RedditClientFactory.AddRedditClient(services);

        return services
            .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
            .AddScoped<IPostManager, PostManager>()
            .AddScoped<IPostResolver, PostResolver>()
            .AddScoped<IFlareManager, FlareManager>()
            .BindConfiguration<RedditSettings>("reddit")
            .AddSubredditSettings();
    }

    public static IServiceCollection AddSubredditSettings(this IServiceCollection services)
    {
        return services
            .BindConfiguration<SubredditSettings>("subreddit");
    }
}