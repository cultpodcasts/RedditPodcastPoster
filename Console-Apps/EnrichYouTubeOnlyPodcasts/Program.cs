using System.Diagnostics;
using System.Reflection;
using CommandLine;
using EnrichYouTubeOnlyPodcasts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
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
    .AddCommonServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichYouTubePodcastRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(EnrichYouTubePodcastRequest request)
{
    var processor = host.Services.GetService<EnrichYouTubePodcastProcessor>();
    await processor!.Run(request);
    return 0;
}

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}