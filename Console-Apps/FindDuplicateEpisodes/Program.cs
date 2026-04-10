using System.Reflection;
using CommandLine;
using FindDuplicateEpisodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

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
    .AddScoped<FindDuplicateEpisodesProcessor>();

using var host = builder.Build();
return await Parser.Default.ParseArguments<FindDuplicateEpisodesRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1));

async Task<int> Run(FindDuplicateEpisodesRequest request)
{
    var processor = host.Services.GetService<FindDuplicateEpisodesProcessor>()!;
    await processor.Run(request);
    return 0;
}

