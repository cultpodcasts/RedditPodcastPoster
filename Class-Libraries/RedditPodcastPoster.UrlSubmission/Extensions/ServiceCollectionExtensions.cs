using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;

namespace RedditPodcastPoster.UrlSubmission.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUrlSubmission(this IServiceCollection services)
    {
        return services
            .AddScoped<IUrlCategoriser, UrlCategoriser>()
            .AddScoped<IAppleUrlCategoriser, AppleUrlCategoriser>()
            .AddScoped<ISpotifyUrlCategoriser, SpotifyUrlCategoriser>()
            .AddScoped<IYouTubeUrlCategoriser, YouTubeUrlCategoriser>()
            .AddScoped<IUrlSubmitter, UrlSubmitter>()
            .AddScoped<IUrlSubmitterV2, UrlSubmitterV2>()
            .AddScoped<IPodcastService, PodcastService>()
            .AddSingleton<IDescriptionHelper, DescriptionHelper>()
            .AddSingleton<IEpisodeHelper, EpisodeHelper>()
            .AddScoped<IEpisodeFactory, EpisodeFactory>()
            .AddScoped<IEpisodeEnricher, EpisodeEnricher>()
            .AddScoped<IPodcastAndEpisodeFactory, PodcastAndEpisodeFactory>()
            .AddScoped<IPodcastAndEpisodeFactoryV2, PodcastAndEpisodeFactoryV2>()
            .AddScoped<IPodcastProcessor, PodcastProcessor>()
            .AddScoped<IPodcastProcessorV2, PodcastProcessorV2>()
            .AddScoped<ICategorisedItemProcessor, CategorisedItemProcessor>()
            .AddScoped<ICategorisedItemProcessorV2, CategorisedItemProcessorV2>()
            .AddScoped<IDiscoveryUrlSubmitter, DiscoveryUrlSubmitter>()
            .AddScoped<IDiscoveryResultProcessor, DiscoveryResultProcessor>()
            .AddSingleton<ISubmitResultAdaptor, SubmitResultAdaptor>();
    }
}