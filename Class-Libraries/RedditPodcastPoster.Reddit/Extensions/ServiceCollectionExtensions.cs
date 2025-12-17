using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRedditServices()
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

        public IServiceCollection AddSubredditSettings()
        {
            return services
                .BindConfiguration<SubredditSettings>("subreddit");
        }
    }
}