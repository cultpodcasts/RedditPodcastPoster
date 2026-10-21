using Microsoft.Extensions.DependencyInjection;
using idunno.Bluesky;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
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
            .AddScoped<IBlueskyPlatformCardSource, BlueskyPlatformCardSource>()
            .AddSingleton<IBlueskyCardImageDownloader, BlueskyCardImageDownloader>()
            .AddScoped<IBlueskyFeedClient, BlueskyFeedClient>()
            .AddScoped<IBlueskyEmbedCardPostFactory, BlueskyEmbedCardPostFactory>()
            .AddScoped<IBlueskyPoster, BlueskyPoster>()
            .AddScoped<IBlueskyPostManager, BlueskyPostManager>()
            .AddSingleton<IBlueskyAgentOptionsFactory, BlueskyAgentOptionsFactory>()
            .AddSingleton(sp => sp.GetRequiredService<IBlueskyAgentOptionsFactory>().Create(async (handle, token) =>
            {
                var agent = await sp.GetRequiredService<IAsyncInstance<BlueskyAgent>>()
                    .GetAsync(token)
                    .ConfigureAwait(false);
                return await agent.ResolveHandle(handle, token).ConfigureAwait(false);
            }))
            .AddSingleton<IBlueskyAgentFactory, BlueskyAgentFactory>()
            // BlueskyAgent is from external library (idunno.Bluesky), so we use the concrete type here
            // rather than creating a wrapper interface
            .AddSingleton<IAsyncInstance<BlueskyAgent>>(x => 
                new AsyncInstance<BlueskyAgent>(x.GetService<IBlueskyAgentFactory>()!))
            .BindConfiguration<BlueskyOptions>("bluesky");
    }
}
