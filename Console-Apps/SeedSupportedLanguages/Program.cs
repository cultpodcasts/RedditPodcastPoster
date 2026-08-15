using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Configuration;

if (args.Contains("--version"))
{
    VersionInfo.PrintVersion();
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
    .AddContentPublishing()
    .AddSingleton<SeedSupportedLanguagesProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<SeedSupportedLanguagesRequest>(args)
    .MapResult(async request =>
    {
        var processor = host.Services.GetRequiredService<SeedSupportedLanguagesProcessor>();
        return await processor.Run(request);
    },
    _ => Task.FromResult(1));
