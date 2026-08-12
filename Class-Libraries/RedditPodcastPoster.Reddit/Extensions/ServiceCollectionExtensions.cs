using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Reddit.Configuration;
using RedditPodcastPoster.Reddit.Factories;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRedditServices()
        {
            // Live Reddit.NET posting/un-post/flair are detached. Keep title/comment
            // constructors and settings for a future Devvit poster; RunPoster switches stay.
            return services
                .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
                .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
                .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
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
