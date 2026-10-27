using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using iTunesSearch.Library;

namespace Index;

/// <summary>
/// Service registration for the Index console app, shared by Program.cs and the composition tests
/// so a missing registration fails a unit test instead of crashing the CLI at startup.
/// </summary>
public static class IndexServices
{
    public static IServiceCollection AddIndexConsoleServices(this IServiceCollection services)
    {
        services
            .AddLogging()
            .AddSingleton<IndexProcessor>()
            .AddEpisodesDomain().AddEpisodeLookup()
            .AddRepositories()
            .AddCatalogueServices()
            .AddSocialPostingServices()
            .AddPodcastServices()
            .AddNonPodcastScrapers()
            .AddEdgeApiClient(bypassCertificateValidation: false)
            .AddAppleServices()
            .AddYouTubeServices(ApplicationUsage.Cli)
            .AddSpotifyServices()
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddEliminationTerms()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddTextSanitiser()
            .AddPeopleServices()
            .AddIndexer()
            .AddEpisodeSearchIndexerService()
            .AddHttpClient();

        services.AddPostingCriteria();
        services.AddDelayedYouTubePublication();
        return services;
    }
}
