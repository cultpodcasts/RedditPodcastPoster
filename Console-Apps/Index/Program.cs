using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using Index;
using iTunesSearch.Library;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
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
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddSingleton<IndexProcessor>()
    .AddEpisodesDomain()
    .AddRepositories()
    .AddCatalogueServices()
    .AddSocialPostingServices()
    .AddPodcastServices()
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

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();

using var host = builder.Build();

return await Parser.Default.ParseArguments<IndexRequest>(args)
    .MapResult(async indexRequest => await Run(indexRequest), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(IndexRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var urlSubmitter = host.Services.GetService<IndexProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}
