using System.Reflection;
using CommandLine;
using DeleteSearchDocument;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.Search.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSearch()
    .AddScoped<DeleteSearchDocumentProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<DeleteSearchDocumentRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(DeleteSearchDocumentRequest request)
{
    var podcastProcessor = host.Services.GetService<DeleteSearchDocumentProcessor>()!;
    await podcastProcessor.Process(request);
    return 0;
}