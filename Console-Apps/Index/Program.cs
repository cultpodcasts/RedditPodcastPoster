using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using Index;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

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

builder.Services.AddIndexConsoleServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<IndexRequest>(args)
    .MapResult(async indexRequest => await Run(indexRequest), errs => Task.FromResult(-1));

async Task<int> Run(IndexRequest request)
{
    var urlSubmitter = host.Services.GetService<IndexProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}
