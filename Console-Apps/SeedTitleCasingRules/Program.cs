using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using SeedTitleCasingRules;
using RedditPodcastPoster.Configuration.Extensions;
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
    .AddSingleton<SeedTitleCasingRulesProcessor>();


using var host = builder.Build();
return await Parser.Default.ParseArguments<SeedTitleCasingRulesRequest>(args)
    .MapResult(async request => await Run(request), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(SeedTitleCasingRulesRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var urlSubmitter = host.Services.GetService<SeedTitleCasingRulesProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}
