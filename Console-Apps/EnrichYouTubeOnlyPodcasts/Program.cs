using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using EnrichYouTubeOnlyPodcasts;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

var appDirectory = AppContext.BaseDirectory;
builder.Environment.ContentRootPath = appDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(appDirectory, "appsettings.json"), true)
    .AddJsonFile(Path.Combine(appDirectory, "EnrichYouTubeOnlyPodcasts.appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddEpisodesDomain()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddRepositories()
    .AddFileRepository()
    .AddSubjectServices()
    .AddTextSanitiser()
    .AddCachedSubjectProvider()
    .AddSingleton<EnrichYouTubePodcastProcessor>()
    .AddPostingCriteria()
    .AddEliminationTerms()
    .AddEpisodeSearchIndexerService()
    .AddCatalogueServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichYouTubePodcastRequest>(args)
    .MapResult(async request => await Run(request), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(EnrichYouTubePodcastRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var processor = host.Services.GetService<EnrichYouTubePodcastProcessor>();
    await processor!.Run(request);
    return 0;
}