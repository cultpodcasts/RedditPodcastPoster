using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

if (args.Any(IsHelpArg))
{
    PrintUsage();
    return 0;
}

var publishTarget = ParsePublishTarget(args);
if (publishTarget is null)
{
    Console.Error.WriteLine($"Unknown argument(s): {string.Join(' ', args)}");
    PrintUsage();
    return 1;
}

var appDirectory = AppContext.BaseDirectory;
var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = appDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(appDirectory, "appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddPeopleServices()
    .AddContentPublishing()
    .AddRedditServices()
    .AddTextSanitiser()
    .AddSubjectServices();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var success = true;

if (publishTarget is PublishTarget.Languages or PublishTarget.All)
{
    var languagesPublisher = scope.ServiceProvider.GetRequiredService<ILanguagesPublisher>();
    success = await languagesPublisher.PublishLanguages();
}

if (success && publishTarget is PublishTarget.People or PublishTarget.All)
{
    var peoplePublisher = scope.ServiceProvider.GetRequiredService<IPeoplePublisher>();
    await peoplePublisher.PublishPeople();
}

if (success && publishTarget is PublishTarget.Flairs or PublishTarget.All)
{
    var subjectsPublisher = scope.ServiceProvider.GetRequiredService<ISubjectsPublisher>();
    await subjectsPublisher.PublishFlairs();
}

return success ? 0 : 1;

static PublishTarget? ParsePublishTarget(string[] args)
{
    var tokens = args
        .Where(a => !a.Contains('=', StringComparison.Ordinal))
        .Select(a => a.Trim())
        .Where(a => a.Length > 0)
        .ToArray();

    if (tokens.Length == 0)
    {
        return PublishTarget.Languages;
    }

    if (tokens.Length > 1)
    {
        return null;
    }

    return tokens[0].ToLowerInvariant() switch
    {
        "languages" or "--languages" or "-l" => PublishTarget.Languages,
        "people" or "--people" or "-p" => PublishTarget.People,
        "flairs" or "--flairs" or "-f" or "flair" => PublishTarget.Flairs,
        "all" or "--all" or "-a" => PublishTarget.All,
        _ => null
    };
}

static bool IsHelpArg(string arg) =>
    arg is "--help" or "-h" or "-?" or "help";

static void PrintUsage()
{
    Console.WriteLine("""
        PublishR2 — publish static content to Cloudflare R2 and Reddit flairs.

        Usage:
          PublishR2 [languages|people|flairs|all]
          PublishR2 [--languages|-l|--people|-p|--flairs|-f|--all|-a]

        Targets:
          languages (default)  Publish languages list to R2
          people               Publish People register to R2 (Cosmos read → R2 write)
          flairs               Publish subject flairs to Reddit
          all                  Publish languages, people, then flairs

        Examples:
          PublishR2
          PublishR2 people
          PublishR2 --flairs
          PublishR2 all
        """);
}

enum PublishTarget
{
    Languages,
    People,
    Flairs,
    All
}
