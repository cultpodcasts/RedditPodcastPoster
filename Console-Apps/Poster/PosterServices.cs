using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;

namespace Poster;

/// <summary>
/// Service registration for the Poster console app, shared by Program.cs and the composition tests
/// so a missing registration fails a unit test instead of crashing the CLI at startup.
/// </summary>
public static class PosterServices
{
    public static IServiceCollection AddPosterConsoleServices(this IServiceCollection services)
    {
        services
            .AddLogging()
            .AddScoped<PostProcessor>()
            .AddEpisodesDomain().AddEpisodeLookup()
            .AddRepositories()
            .AddCatalogueServices()
            .AddSocialPostingServices()
            .AddPodcastServices()
            .AddPeopleServices()
            .AddContentPublishing()
            .AddTwitterServices()
            .AddBlueskyServices()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddTextSanitiser()
            .AddCloudflareClients()
            .AddShortnerServices()
            .AddYouTubeServices(ApplicationUsage.Cli)
            .AddCachedSubjectProvider()
            .AddSpotifyServices()
            .AddHttpClient();

        services.AddPostingCriteria();
        services.AddDelayedYouTubePublication();
        return services;
    }
}
