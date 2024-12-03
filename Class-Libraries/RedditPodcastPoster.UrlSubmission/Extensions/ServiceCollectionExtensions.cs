using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;

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
            .AddScoped<IPodcastService, PodcastService>()
            .AddSingleton<IDescriptionHelper, DescriptionHelper>()
            .AddSingleton<IEpisodeHelper, EpisodeHelper>()
            .AddScoped<IEpisodeFactory, EpisodeFactory>()
            .AddScoped<IEpisodeEnricher, EpisodeEnricher>()
            .AddScoped<IPodcastAndEpisodeFactory, PodcastAndEpisodeFactory>()
            .AddScoped<IPodcastProcessor, PodcastProcessor>()
            .AddScoped<ICategorisedItemProcessor, CategorisedItemProcessor>()
            .AddScoped<IDiscoveryUrlSubmitter, DiscoveryUrlSubmitter>()
            .AddScoped<IDiscoveryResultProcessor, DiscoveryResultProcessor>()
            .AddSingleton<ISubmitResultAdaptor, SubmitResultAdaptor>();
    }
}