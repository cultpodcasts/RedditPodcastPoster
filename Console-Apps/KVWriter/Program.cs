using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using KVWriter;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.Configuration;

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
    .AddCloudflareClients()
    .AddShortnerServices()
    .AddScoped<KVWriterProcessor>()
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<KVWriterRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(KVWriterRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var processor = host.Services.GetService<KVWriterProcessor>()!;
    await processor.Process(request);
    return 0;
}