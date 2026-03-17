using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRedditServices()
        {
            RedditClientFactory.AddRedditClient(services);
            services.AddHttpClient<IDevvitClient, DevvitClient>();

            return services
                .AddAuth0Client()
                .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
                .AddScoped<RedditLinkPoster>()
                .AddScoped<DevvitRedditLinkPoster>()
                .AddScoped<IRedditLinkPoster, ConfiguredRedditLinkPoster>()
                .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
                .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
                .AddScoped<IPostManager, PostManager>()
                .AddScoped<IPostResolver, PostResolver>()
                .AddScoped<IFlareManager, FlareManager>()
                .BindConfiguration<RedditSettings>("reddit")
                .BindConfiguration<DevvitSettings>("devvit")
                .AddSubredditSettings();
        }

        public IServiceCollection AddSubredditSettings()
        {
            return services
                .BindConfiguration<SubredditSettings>("subreddit");
        }
    }
}