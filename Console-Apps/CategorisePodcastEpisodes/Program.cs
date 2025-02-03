using System.Reflection;
using CategorisePodcastEpisodes;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
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
    .AddScoped<CategorisePodcastEpisodesProcessor>()
    .AddCachedSubjectProvider()
    .AddSubjectServices()
    .AddTextSanitiser();

using var host = builder.Build();

return await Parser.Default.ParseArguments<CategorisePodcastEpisodesRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(CategorisePodcastEpisodesRequest request)
{
    var urlSubmitter = host.Services.GetService<CategorisePodcastEpisodesProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}