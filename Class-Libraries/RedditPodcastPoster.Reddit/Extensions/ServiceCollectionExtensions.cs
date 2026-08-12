using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Reddit.Configuration;
using RedditPodcastPoster.Reddit.Episodes;
using RedditPodcastPoster.Reddit.Factories;
using RedditPodcastPoster.Reddit.Managers;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRedditServices()
        {
            // Reddit.NET client factories removed — posting/un-post/flair are no-ops.
            // Keep RunPoster / website reddit switches and these ports for a future Devvit poster.
            return services
                .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
                .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
                .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
                .AddScoped<IPostManager, PostManager>()
                .AddScoped<IEpisodePostManager, EpisodePostManager>()
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
