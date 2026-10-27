using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CommandLine;
using EpisodeServiceBackfill;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Persistence.Repositories;
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

builder.Services
    .AddLogging()
    .AddRepositories()
        .AddSingleton<IEpisodeCatalogPatchSource, LeftoverEpisodeCatalogPatchSource>()
        .AddSingleton<IBackfillEpisodeRepository>(s =>
        {
            var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
            return new BackFillEpisodeRepository(
                containerFactory.CreateEpisodesContainer(),
                s.GetRequiredService<ILookupRepository>(),
                s.GetRequiredService<IPodcastRepository>(),
                s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EpisodeRepository>>());
        })
        .AddSingleton<EpisodeServiceBackfillProcessor>()
    .AddSingleton<EpisodeServiceBackfillHost>()
    .AddSingleton<IEpisodeYouTubeIdRepairStore, CosmosEpisodeYouTubeIdRepairStore>()
    .AddSingleton<EpisodeYouTubeIdRepairRunner>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EpisodeServiceBackfillRequest>(args)
    .MapResult(async request =>
    {
        if (request.YouTubeIdOnly)
        {
            return await host.Services.GetRequiredService<EpisodeYouTubeIdRepairRunner>().Run(request);
        }

        var runner = host.Services.GetRequiredService<EpisodeServiceBackfillHost>();
        return await runner.Run(request);
    },
    errs => Task.FromResult(1));
