using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedditServices(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<RedditSettings>("reddit");
        services.AddSubredditSettings(config);

        RedditClientFactory.AddRedditClient(services);

        return services.AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>();
    }

    public static void AddSubredditSettings(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<SubredditSettings>("subreddit");
    }
}