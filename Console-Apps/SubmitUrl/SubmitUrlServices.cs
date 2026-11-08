using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace SubmitUrl;

/// <summary>
/// Service registration for the SubmitUrl console app, shared by Program.cs and the composition tests
/// so a missing registration fails a unit test instead of crashing the CLI at startup.
/// </summary>
public static class SubmitUrlServices
{
    public static IServiceCollection AddSubmitUrlConsoleServices(
        this IServiceCollection services,
        bool useRefreshMetaEnricher)
    {
        services
            .AddLogging()
            .AddEpisodesDomain().AddEpisodeLookup()
            .AddRepositories()
            .AddCatalogueServices()
            .AddPodcastServices()
            .AddEdgeApiClient(bypassCertificateValidation: false)
            .AddSpotifyServices()
            .AddAppleServices()
            .AddYouTubeServices(ApplicationUsage.Cli)
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped(_ => new iTunesSearchManager())
            .AddPeopleServices()
            .AddUrlSubmission(useRefreshMetaEnricher: useRefreshMetaEnricher)
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddTextSanitiser()
            .AddScoped<SubmitUrlProcessor>()
            .AddEpisodeSearchIndexerService()
            .AddNonPodcastScrapers()
            .AddHttpClient();

        services.AddPostingCriteria();
        return services;
    }
}
