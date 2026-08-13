using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Factories;

namespace RedditPodcastPoster.SocialPosting.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSocialPostingServices(this IServiceCollection services)
    {
        services.AddDelayedYouTubePublication();

        // Reddit episode posting (EpisodeProcessor / PodcastEpisodePoster) removed with Reddit.NET.
        // Keep candidacy filter, recent candidates, and PostModelFactory for Tweet/Bluesky.
        return services
            .AddSingleton<IPodcastEpisodeFilter, PodcastEpisodeFilter>()
            .AddSingleton<IRecentEpisodeCandidatesProvider, RecentEpisodeCandidatesProvider>()
            .AddScoped<IPodcastEpisodeProvider, PodcastEpisodeProvider>()
            .AddScoped<IPostModelFactory, PostModelFactory>();
    }
}
