using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace EnrichExistingEpisodesFromPodcastServices;

/// <summary>
/// Service registration for the EnrichExistingEpisodesFromPodcastServices console app, shared by Program.cs
/// and the composition tests so a missing registration fails a unit test instead of crashing the CLI at startup.
/// </summary>
public static class EnrichExistingEpisodesServices
{
    public static IServiceCollection AddEnrichExistingEpisodesConsoleServices(this IServiceCollection services)
    {
        return services
            .AddLogging()
            .AddEpisodesDomain().AddEpisodeLookup()
            .AddRepositories()
            .AddScoped<EnrichPodcastEpisodesProcessor>()
            .AddUrlSubmission()
            .AddCatalogueServices()
            .AddSocialPostingServices()
            .AddSpotifyServices()
            .AddYouTubeServices(ApplicationUsage.Cli)
            .AddAppleServices()
            .AddRemoteClient()
            .AddTextSanitiser()
            .AddScoped(_ => new iTunesSearchManager())
            .AddEpisodeSearchIndexerService()
            .AddHttpClient();
    }
}
