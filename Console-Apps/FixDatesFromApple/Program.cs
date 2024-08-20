using System.Reflection;
using CommandLine;
using FixDatesFromApple;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddSingleton<Processor>()
    .AddRepositories()
    .AddAppleServices()
    .AddHttpClient();


using var host = builder.Build();

return await Parser.Default.ParseArguments<FixRequest>(args)
    .MapResult(async indexRequest => await Run(indexRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(FixRequest request)
{
    var urlSubmitter = host.Services.GetService<Processor>()!;
    await urlSubmitter.Run(request);
    return 0;
}