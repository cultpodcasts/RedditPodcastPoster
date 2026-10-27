using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using iTunesSearch.Library;

namespace AddAudioPodcast;

/// <summary>
/// Service registration for the AddAudioPodcast console app, shared by Program.cs and the composition tests
/// so a missing registration fails a unit test instead of crashing the CLI at startup.
/// </summary>
public static class AddAudioPodcastServices
{
    public static IServiceCollection AddAddAudioPodcastConsoleServices(this IServiceCollection services)
    {
        services
            .AddLogging()
            .AddScoped<AddAudioPodcastProcessor>()
            .AddEpisodesDomain().AddEpisodeLookup()
            .AddRepositories()
            .AddCatalogueServices()
            .AddPodcastServices()
            .AddAppleServices()
            .AddSpotifyServices()
            .AddYouTubeServices(ApplicationUsage.Cli)
            .AddEliminationTerms()
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddTextSanitiser()
            .AddEpisodeSearchIndexerService()
            .AddHttpClient();

        services.AddPostingCriteria();
        services.AddDelayedYouTubePublication();
        return services;
    }
}
