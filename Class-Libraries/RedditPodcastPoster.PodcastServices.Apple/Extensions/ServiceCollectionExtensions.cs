using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

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