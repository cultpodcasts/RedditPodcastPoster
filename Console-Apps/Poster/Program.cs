using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using Poster;
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

builder.Services.AddPosterConsoleServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<PostRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1));

async Task<int> Run(PostRequest request)
{
    var postProcessor = host.Services.GetService<PostProcessor>()!;
    await postProcessor.Process(request);
    return 0;
}
