using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.UrlSubmission;

namespace RedditPodcastPoster.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPodcastServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDelayedYouTubePublication(config);

        return services
            .AddScoped<IPodcastService, PodcastService>()
            .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
            .AddScoped<IPodcastUpdater, PodcastUpdater>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddScoped<IAppleEpisodeRetrievalHandler, AppleEpisodeRetrievalHandler>()
            .AddScoped<IYouTubeEpisodeRetrievalHandler, YouTubeEpisodeRetrievalHandler>()
            .AddScoped<ISpotifyEpisodeRetrievalHandler, SpotifyEpisodeRetrievalHandler>()
            .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
            .AddScoped<IEpisodePostManager, EpisodePostManager>()
            .AddScoped<IPodcastEpisodesPoster, PodcastEpisodesPoster>()
            .AddScoped<IPodcastEpisodePoster, PodcastEpisodePoster>()
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IPodcastEpisodeFilter, PodcastEpisodeFilter>()
            .AddSingleton<IProcessResponsesAdaptor, ProcessResponsesAdaptor>();
    }
}