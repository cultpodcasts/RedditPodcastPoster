using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CategorisePodcastEpisodes;
using CommandLine;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
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
    .AddScoped<CategorisePodcastEpisodesProcessor>()
    .AddCachedSubjectProvider()
    .AddSubjectServices()
    .AddTextSanitiser()
    .AddCatalogueServices()
    .AddSocialPostingServices()
    .AddEpisodeSearchIndexerService();

using var host = builder.Build();

return await Parser.Default.ParseArguments<CategorisePodcastEpisodesRequest>(args)
    .MapResult(async request => await Run(request), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(CategorisePodcastEpisodesRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var service = host.Services.GetService<CategorisePodcastEpisodesProcessor>()!;
    await service.Run(request);
    return 0;
}