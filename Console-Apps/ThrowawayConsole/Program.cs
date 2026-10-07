using System.Diagnostics;
using System.Reflection;
using idunno.AtProto;
using idunno.Bluesky;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.DependencyInjection;

if (args.Contains("--version"))
{
    VersionInfo.PrintVersion();
    return 0;
}

if (args.Length != 1 || !AtUri.TryParse(args[0], out var atUri) || atUri.RecordKey is null)
{
    Console.Error.WriteLine("Usage: ThrowawayConsole <bluesky-at-uri>");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .SetBasePath(GetBasePath())
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddBlueskyServices();

using var host = builder.Build();
var agent = await host.Services.GetRequiredService<IAsyncInstance<BlueskyAgent>>().GetAsync();
var deleted = await agent.DeletePost(atUri);
if (!deleted.Succeeded)
{
    Console.Error.WriteLine(
        $"Bluesky delete failed. Status-code: {deleted.StatusCode}. Error: {deleted.AtErrorDetail?.Error}. Message: {deleted.AtErrorDetail?.Message}.");
    return 1;
}

Console.WriteLine($"Deleted Bluesky post {atUri}");
return 0;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
