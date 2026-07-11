using Microsoft.Extensions.DependencyInjection;
using idunno.Bluesky;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.YouTube;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.People.Extensions;

namespace RedditPodcastPoster.Bluesky.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Bluesky posting services. Also registers People services required by
    /// <c>BlueskyEmbedCardPostFactory</c> (<c>IPersonGuestHandleResolver</c>).
    /// </summary>
    public static IServiceCollection AddBlueskyServices(this IServiceCollection services)
    {
        return services
            .AddPeopleServices()
            .AddSingleton<IBlueskyClientFactory, BlueskyClientFactory>()
            .AddSingleton(x => x.GetService<IBlueskyClientFactory>()!.Create())
            .AddScoped<IBlueskyEmbedCardPostFactory, BlueskyEmbedCardPostFactory>()
            .AddScoped<IBlueskyPoster, BlueskyPoster>()
            .AddScoped<IBlueskyPostManager, BlueskyPostManager>()
            .AddScoped<IEmbedCardRequestFactory, EmbedCardRequestFactory>()
            .AddScoped<IBlueskyYouTubeServiceFactory, BlueskyYouTubeServiceFactory>()
            .AddScoped(s => s.GetService<IBlueskyYouTubeServiceFactory>()!.Create())
            .AddSingleton<IBlueskyAgentFactory, BlueskyAgentFactory>()
            // BlueskyAgent is from external library (idunno.Bluesky), so we use the concrete type here
            // rather than creating a wrapper interface
            .AddSingleton<IAsyncInstance<BlueskyAgent>>(x => 
                new AsyncInstance<BlueskyAgent>(x.GetService<IBlueskyAgentFactory>()!))
            .BindConfiguration<BlueskyOptions>("bluesky");
    }
}