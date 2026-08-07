using System.Reflection;
using CommandLine;
using EpisodeUpdateTimingProbe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

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
    .AddTextSanitiser()
    .AddSubjectServices()
    .AddContentPublishing()
    .AddEpisodeSearchIndexerService()
    .AddScoped<EpisodeUpdateTimingProbeProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EpisodeUpdateTimingProbeRequest>(args)
    .MapResult(async request =>
    {
        using var scope = host.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<EpisodeUpdateTimingProbeProcessor>();
        return await processor.Run(request, CancellationToken.None);
    },
    _ => Task.FromResult(1));
