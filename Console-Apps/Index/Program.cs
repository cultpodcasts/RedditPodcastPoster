using System.Reflection;
using CommandLine;
using Index;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

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
    .AddRepositories()
    .AddCommonServices()
    .AddPodcastServices()
    .AddAppleServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddSpotifyServices()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddEliminationTerms()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddTextSanitiser()
    .AddIndexer()
    .AddEpisodeSearchIndexerService()
    .AddHttpClient();

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();


using var host = builder.Build();

return await Parser.Default.ParseArguments<IndexRequest>(args)
    .MapResult(async indexRequest => await Run(indexRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(IndexRequest request)
{
    var urlSubmitter = host.Services.GetService<IndexProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}