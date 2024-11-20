using System.Reflection;
using CommandLine;
using IndexAllEpisodesAudit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
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
    .AddScoped<IndexAllEpisodesAuditProcessor>();

builder.Services.AddPostingCriteria();

using var host = builder.Build();
return await Parser.Default.ParseArguments<IndexAllEpisodesAuditRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(IndexAllEpisodesAuditRequest request)
{
    var processor = host.Services.GetService<IndexAllEpisodesAuditProcessor>()!;
    await processor.Process(request);
    return 0;
}