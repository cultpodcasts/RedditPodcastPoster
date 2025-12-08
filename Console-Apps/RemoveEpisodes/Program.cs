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

return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1));

async Task<int> Run(Request request)
{
    var podcastProcessor = host.Services.GetService<Processor>()!;
    await podcastProcessor.Process(request);
    return 0;
}