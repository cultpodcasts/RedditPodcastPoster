using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using EnrichExistingEpisodesFromPodcastServices;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services.AddEnrichExistingEpisodesConsoleServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichPodcastEpisodesRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1));

async Task<int> Run(EnrichPodcastEpisodesRequest request)
{
    var urlSubmitter = host.Services.GetService<EnrichPodcastEpisodesProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}
