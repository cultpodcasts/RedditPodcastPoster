using System.Reflection;
using CommandLine;
using FilmSearchPusher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
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
    .AddFilmSearchPush();
builder.Services.TryAddSingleton(TimeProvider.System);

using var host = builder.Build();

return await Parser.Default.ParseArguments<FilmSearchPusherRequest>(args)
    .MapResult(async request => await Run(request),
        _ => Task.FromResult(-1));

async Task<int> Run(FilmSearchPusherRequest request)
{
    using var scope = host.Services.CreateScope();
    var pusher = scope.ServiceProvider.GetRequiredService<IFilmSearchPusher>();
    var result = await pusher.Push(FilmSearchPusherCommand.ToPushRequest(request), CancellationToken.None);
    return FilmSearchPusherCommand.ExitCode(result);
}
