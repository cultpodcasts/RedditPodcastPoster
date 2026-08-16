using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using DeleteSearchDocument;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Search.Extensions;
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
    .AddRepositories()
    .AddSearch()
    .AddScoped<DeleteSearchDocumentProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<DeleteSearchDocumentRequest>(args)
    .MapResult(async request => await Run(request),
        errs =>
        {
            if (errs.Any(x => x is VersionRequestedError))
            {
                VersionInfo.PrintVersion();
                return Task.FromResult(0);
            }

            return Task.FromResult(-1);
        });

async Task<int> Run(DeleteSearchDocumentRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var podcastProcessor = host.Services.GetService<DeleteSearchDocumentProcessor>()!;
    await podcastProcessor.Process(request);
    return 0;
}