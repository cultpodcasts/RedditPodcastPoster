using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

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
    .AddBBCServices()
    .AddHttpClient();

using var host = builder.Build();

if (args.Length == 0 || !Uri.TryCreate(args[0], UriKind.Absolute, out var url))
{
    Console.Error.WriteLine("Usage: ThrowawayConsole <bbc-url>");
    return 1;
}

var service = host.Services.GetRequiredService<IBBCPageMetaDataExtractor>();
var pageData = await service.GetMetaData(url);

Console.WriteLine($"Title: {pageData.Title}");
Console.WriteLine($"Description: {pageData.Description}");

return 0;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
