using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.SocialPosting.Adaptors;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Factories;

namespace RedditPodcastPoster.SocialPosting.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSocialPostingServices(this IServiceCollection services)
    {
        services.AddDelayedYouTubePublication();

        return services
            .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
            .AddScoped<IPodcastEpisodesPoster, PodcastEpisodesPoster>()
            .AddScoped<IPodcastEpisodePoster, PodcastEpisodePoster>()
            .AddSingleton<IPodcastEpisodeFilter, PodcastEpisodeFilter>()
            .AddSingleton<IRecentEpisodeCandidatesProvider, RecentEpisodeCandidatesProvider>()
            .AddSingleton<IProcessResponsesAdaptor, ProcessResponsesAdaptor>()
            .AddScoped<IPodcastEpisodeProvider, PodcastEpisodeProvider>()
            .AddScoped<IPostModelFactory, PostModelFactory>();
    }
}
