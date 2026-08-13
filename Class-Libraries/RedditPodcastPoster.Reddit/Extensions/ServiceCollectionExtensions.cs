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
            // Not used by Indexer/Api/Discovery/Poster/PublishR2 hosts — they are detached
            // from Reddit DI. Kept for unit tests and a future Devvit poster host.
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
