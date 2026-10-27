using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewsOrganisationDefaultsBackfill;
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
    .AddSingleton<NewsOrganisationDefaultsBackfillProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<NewsOrganisationDefaultsBackfillRequest>(args)
    .MapResult(async request =>
        {
            var processor = host.Services.GetRequiredService<NewsOrganisationDefaultsBackfillProcessor>();
            var result = await processor.Run(request);
            return result.ExitCode;
        },
        errs => Task.FromResult(1));
