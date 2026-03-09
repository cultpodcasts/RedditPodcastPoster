using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddDelayedYouTubePublication();

        return services
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
            .AddScoped<IEpisodePostManager, EpisodePostManager>()
            .AddScoped<IPodcastEpisodesPoster, PodcastEpisodesPoster>()
            .AddScoped<IPodcastEpisodePoster, PodcastEpisodePoster>()
            .AddScoped<IPodcastEpisodePosterV2, PodcastEpisodePosterV2>()
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IPodcastFilterV2, PodcastFilterV2>()
            .AddSingleton<IPodcastEpisodeFilter, PodcastEpisodeFilter>()
            .AddSingleton<IPodcastEpisodeFilterV2, PodcastEpisodeFilterV2>()
            .AddSingleton<IProcessResponsesAdaptor, ProcessResponsesAdaptor>()
            .AddScoped<IPodcastEpisodeProvider, PodcastEpisodeProvider>()
            .AddScoped<IPodcastEpisodeProviderV2, PodcastEpisodeProviderV2>()
            .AddScoped<IPostModelFactory, PostModelFactory>()
            .AddScoped<IPodcastFactory, PodcastFactory>();
    }
}