using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
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
            .AddSingleton<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IPodcastEpisodeFilter, PodcastEpisodeFilter>()
            .AddSingleton<IProcessResponsesAdaptor, ProcessResponsesAdaptor>()
            .AddScoped<IPodcastEpisodeProvider, PodcastEpisodeProvider>()
            .AddScoped<IPodcastFactory, PodcastFactory>();
    }
}