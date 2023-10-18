using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.Apple.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppleServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IAppleEpisodeEnricher, AppleEpisodeEnricher>()
            .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
            .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
            .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
            .AddScoped<IApplePodcastService, ApplePodcastService>()
            .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
            .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
            .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
            .AddSingleton<IApplePodcastHttpClientFactory, ApplePodcastHttpClientFactory>();
    }
}