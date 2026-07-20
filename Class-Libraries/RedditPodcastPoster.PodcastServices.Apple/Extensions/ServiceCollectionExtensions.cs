using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Clients;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Apple.Handlers;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;

namespace RedditPodcastPoster.PodcastServices.Apple.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppleServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IAppleEpisodeEnricher, AppleEpisodeEnricher>()
            .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
            .AddScoped<IEnrichedApplePodcastResolver, EnrichedApplePodcastResolver>()
            .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
            .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
            .AddScoped<IApplePodcastService, ApplePodcastService>()
            .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
            .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
            .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
            .AddSingleton<IApplePodcastHttpClientFactory, ApplePodcastHttpClientFactory>()
            .AddSingleton<IAsyncInstance<HttpClient>>(x => 
                new AsyncInstance<HttpClient>(x.GetService<IApplePodcastHttpClientFactory>()!))
            .AddScoped<IAppleEpisodeRetrievalHandler, AppleEpisodeRetrievalHandler>();
    }
}
