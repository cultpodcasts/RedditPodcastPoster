using System.Reflection;
using CommandLine;
using EpisodeWireRewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
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
    .AddSingleton<IWireCutoverProgressReporter, ConsoleWireCutoverProgressReporter>()
    .AddSingleton<IWireCutoverEpisodeStore>(sp =>
    {
        var container = sp.GetRequiredService<ICosmosDbContainerFactory>().CreateEpisodesContainer();
        return new CosmosWireCutoverEpisodeStore(
            container,
            sp.GetRequiredService<ILogger<CosmosWireCutoverEpisodeStore>>());
    })
    .AddSingleton<WireCutoverProcessor>()
    .AddSingleton<EpisodeWireRewriteHost>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EpisodeWireRewriteRequest>(args)
    .MapResult(async request =>
    {
        if (request.Version)
        {
            VersionInfo.PrintVersion();
            return 0;
        }

        var runner = host.Services.GetRequiredService<EpisodeWireRewriteHost>();
        return await runner.Run(request);
    },
    _ => Task.FromResult(1));
