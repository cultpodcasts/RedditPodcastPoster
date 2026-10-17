using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CultPodcasts.DatabasePublisher;
using CultPodcasts.DatabasePublisher.Consistency;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Configuration;
using Spectre.Console;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

PublishOptions options;
try
{
    options = PublishOptions.Parse(args);
}
catch (ArgumentException ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    return 2;
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
    .AddFileRepository()
    .AddRepositories()
    .AddSafeFileWriter()
    .AddSingleton<PublicDatabasePublisher>();

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<PublicDatabasePublisher>();
try
{
    await publisher.Run(options);
}
catch (PublicDatabaseConsistencyException ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    return 1;
}

return 0;
