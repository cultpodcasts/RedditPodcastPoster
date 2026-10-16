using System.Reflection;
using CatalogueMigrateIdentify;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

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
    .AddScoped<CatalogueMigrateIdentifyProcessor>();

using var host = builder.Build();
return await Parser.Default.ParseArguments<CatalogueMigrateIdentifyRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1));

async Task<int> Run(CatalogueMigrateIdentifyRequest request)
{
    var processor = host.Services.GetRequiredService<CatalogueMigrateIdentifyProcessor>();
    var result = await processor.Run(request);
    return result.ExitCode;
}
