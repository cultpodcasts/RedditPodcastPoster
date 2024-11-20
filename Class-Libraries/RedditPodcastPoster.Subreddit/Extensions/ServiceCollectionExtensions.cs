using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Reddit.Extensions;

namespace RedditPodcastPoster.Subreddit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubredditServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSubredditSettings();

        RedditClientFactory.AddRedditClient(services);

        return services
            .AddScoped<ISubredditPostProvider, SubredditPostProvider>()
            .AddScoped<ISubredditRepository, SubredditRepository>();
    }
}