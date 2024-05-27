using System.Reflection;
using CommandLine;
using KVWriter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;

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
    .AddShortnerServices(builder.Configuration)
    .AddScoped<KVWriterProcessor>()
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<KVWriterRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(KVWriterRequest request)
{
    var processor = host.Services.GetService<KVWriterProcessor>()!;
    await processor.Process(request);
    return 0;
}