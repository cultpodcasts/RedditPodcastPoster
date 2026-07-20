using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RemoveEpisodes;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<Processor>()
    .AddRepositories()
    .AddEpisodeSearchIndexerService()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<RemoveRequest, RestoreRequest>(args)
    .MapResult(
        (RemoveRequest request) => RunRemove(request),
        (RestoreRequest request) => RunRestore(request),
        errs => Task.FromResult(-1));

async Task<int> RunRemove(RemoveRequest request)
{
    var processor = host.Services.GetService<Processor>()!;
    await processor.ProcessRemove(request);
    return 0;
}

async Task<int> RunRestore(RestoreRequest request)
{
    var processor = host.Services.GetService<Processor>()!;
    await processor.ProcessRestore(request);
    return 0;
}
