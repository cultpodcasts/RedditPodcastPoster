using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PublishR2;
using RedditPodcastPoster.Configuration.Extensions;
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

var mode = ParseMode(args);
if (mode is null)
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
    .AddSubjectServices()
    .AddScoped<R2PublishProcessor>()
    .AddScoped<FlairPublishProcessor>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

return mode switch
{
    PublishMode.Flairs => await RunFlairs(scope.ServiceProvider),
    PublishMode.All => await RunAll(scope.ServiceProvider),
    _ => await RunR2(scope.ServiceProvider, ToR2Target(mode.Value))
};

async Task<int> RunR2(IServiceProvider services, R2PublishTarget target)
{
    var processor = services.GetRequiredService<R2PublishProcessor>();
    var success = await processor.Process(new R2PublishRequest { Target = target });
    return success ? 0 : 1;
}

async Task<int> RunFlairs(IServiceProvider services)
{
    var processor = services.GetRequiredService<FlairPublishProcessor>();
    await processor.Process(new FlairPublishRequest());
    return 0;
}

async Task<int> RunAll(IServiceProvider services)
{
    var r2Exit = await RunR2(services, R2PublishTarget.All);
    if (r2Exit != 0)
    {
        return r2Exit;
    }

    return await RunFlairs(services);
}

static R2PublishTarget ToR2Target(PublishMode mode) => mode switch
{
    PublishMode.Languages => R2PublishTarget.Languages,
    PublishMode.People => R2PublishTarget.People,
    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
};

static PublishMode? ParseMode(string[] args)
{
    var tokens = args
        .Where(a => !a.Contains('=', StringComparison.Ordinal))
        .Select(a => a.Trim())
        .Where(a => a.Length > 0)
        .ToArray();

    if (tokens.Length == 0)
    {
        return PublishMode.Languages;
    }

    if (tokens.Length > 1)
    {
        return null;
    }

    return tokens[0].ToLowerInvariant() switch
    {
        "languages" or "--languages" or "-l" => PublishMode.Languages,
        "people" or "--people" or "-p" => PublishMode.People,
        "flairs" or "--flairs" or "-f" or "flair" => PublishMode.Flairs,
        "all" or "--all" or "-a" => PublishMode.All,
        "r2" => PublishMode.Languages,
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

        Modes:
          languages (default)  R2PublishProcessor — languages list to R2
          people               R2PublishProcessor — People register to R2
          flairs               FlairPublishProcessor — subject flairs to Reddit
          all                  R2 (languages+people), then flairs

        Examples:
          PublishR2
          PublishR2 people
          PublishR2 --flairs
          PublishR2 all
        """);
}

enum PublishMode
{
    Languages,
    People,
    Flairs,
    All
}
