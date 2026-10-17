using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CultPodcasts.DatabasePublisher;
using CultPodcasts.DatabasePublisher.Consistency;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Configuration;
using Spectre.Console;

// Exit codes: 0 published; 1 the data cannot be published consistently (file-key problem, or a file would
// vanish unexplained, in which case nothing was changed); 2 the arguments or the output folder are unusable.
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
    .AddRepositories()
    .AddSingleton(s => new PublicationFolder(
        Directory.GetCurrentDirectory(),
        s.GetRequiredService<IJsonSerializerOptionsProvider>()))
    .AddSingleton<PublicDatabasePublisher>();

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<PublicDatabasePublisher>();
try
{
    var report = await publisher.Run(options);
    PublicationReportWriter.Write(report);
    return report.Plan.IsBlocked ? 1 : 0;
}
catch (PublicationFolderException ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    return 2;
}
catch (PublicDatabaseConsistencyException ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    return 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    AnsiConsole.MarkupLineInterpolated(
        $"[red]The output folder could not be updated: {ex.Message} Files may be partly updated; check them with git status.[/]");
    return 2;
}
