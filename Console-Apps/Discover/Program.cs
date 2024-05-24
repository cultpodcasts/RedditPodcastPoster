using System.Reflection;
using CommandLine;
using Discover;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddDiscovery(builder.Configuration)
    .AddScoped<DiscoveryProcessor>()
    .AddScoped<IDiscoveryResultConsoleLogger, DiscoveryResultConsoleLogger>()
    .AddRepositories()
    .AddSubjectServices()
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped(s => new iTunesSearchManager())
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<DiscoveryRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(DiscoveryRequest request)
{
    var urlSubmitter = host.Services.GetService<DiscoveryProcessor>()!;
    var result = await urlSubmitter.Process(request);
    if (result.Initiation.HasValue)
    {
        var initiation = $"{result.Initiation:O}";
        Console.WriteLine($"Discovery initiated at '{initiation}'.");
    }

    return 0;
}