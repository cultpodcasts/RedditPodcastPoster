using System.Reflection;
using Azure;
using CommandLine;
using CreateSearchIndex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<ISearchIndexClientFactory, SearchIndexClientFactory>()
    .AddScoped<ISearchIndexerClientFactory, SearchIndexerClientFactory>()
    .AddScoped(s => s.GetService<ISearchIndexClientFactory>()!.Create())
    .AddScoped(s => s.GetService<ISearchIndexerClientFactory>()!.Create())
    .AddScoped<CreateSearchIndexProcessor>();

builder.Services.BindConfiguration<SearchIndexConfig>("searchIndex");
builder.Services.BindConfiguration<CosmosDbSettings>("cosmosdb");

using var host = builder.Build();
return await Parser.Default.ParseArguments<CreateSearchIndexRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(CreateSearchIndexRequest request)
{
    var processor = host.Services.GetService<CreateSearchIndexProcessor>()!;
    await processor.Process(request);
    return 0;
}