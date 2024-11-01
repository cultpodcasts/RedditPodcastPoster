using System.Reflection;
using CommandLine;
using JsonSplitCosmosDbUploader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.JsonSplitCosmosDbUploader;
using RedditPodcastPoster.Persistence.Extensions;

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
    .AddFileRepository("podcast")
    .AddCommonServices(builder.Configuration)
    .AddScoped<JsonSplitCosmosDbUploadProcessor>();


using var host = builder.Build();

return await Parser.Default.ParseArguments<JsonSplitCosmosDbUploadRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(JsonSplitCosmosDbUploadRequest request)
{
    var urlSubmitter = host.Services.GetService<JsonSplitCosmosDbUploadProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}