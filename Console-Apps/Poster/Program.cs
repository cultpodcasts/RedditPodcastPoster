using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Poster;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.ContentPublisher.Extensions;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddContentPublishing(builder.Configuration);

using var host = builder.Build();
return await Parser.Default.ParseArguments<PostRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(PostRequest request)
{
    var urlSubmitter = host.Services.GetService<PostProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}