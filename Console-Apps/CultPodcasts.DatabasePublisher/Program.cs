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
// vanish unexplained, in which case nothing was changed); 2 the arguments or the output folder are unusable
// (nothing was changed); 3 an unexpected failure: nothing was changed unless the message says the folder may be
// partly updated, in which case staged *.json.tmp files remain and the next run needs --discard-staging.
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
catch (PublicationApplyException ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    AnsiConsole.MarkupLine(
        "[red]Check the output folder with git status. Staged *.json.tmp files remain; run again with --discard-staging to delete them.[/]");
    return 3;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]Unexpected failure: {ex.GetType().Name}: {ex.Message}[/]");
    AnsiConsole.MarkupLine("[red]Nothing in the output folder was changed.[/]");
    return 3;
}
