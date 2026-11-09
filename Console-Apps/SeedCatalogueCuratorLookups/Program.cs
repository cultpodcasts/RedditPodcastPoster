using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using SeedCatalogueCuratorLookups;

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

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSingleton<SeedCatalogueCuratorLookupsProcessor>();

using var host = builder.Build();
return await Parser.Default.ParseArguments<SeedCatalogueCuratorLookupsRequest>(args)
    .MapResult(async request => await Run(request), _ => Task.FromResult(-1));

async Task<int> Run(SeedCatalogueCuratorLookupsRequest request)
{
    var processor = host.Services.GetRequiredService<SeedCatalogueCuratorLookupsProcessor>();
    return await processor.Run(request);
}
