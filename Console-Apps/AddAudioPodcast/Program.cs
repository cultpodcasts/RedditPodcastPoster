using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AddAudioPodcast;
using CommandLine;
using iTunesSearch.Library;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
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
    .AddScoped<AddAudioPodcastProcessor>()
    .AddEpisodesDomain()
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

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();

using var host = builder.Build();


return await Parser.Default.ParseArguments<AddAudioPodcastRequest>(args)
    .MapResult(async addAudioPodcastRequest => await Run(addAudioPodcastRequest),
        errs =>
        {
            if (errs.Any(x => x is VersionRequestedError))
            {
                VersionInfo.PrintVersion();
                return Task.FromResult(0);
            }

            return Task.FromResult(-1);
        });

async Task<int> Run(AddAudioPodcastRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var podcastProcessor = host.Services.GetService<AddAudioPodcastProcessor>()!;
    await podcastProcessor.Create(request);
    return 0;
}
