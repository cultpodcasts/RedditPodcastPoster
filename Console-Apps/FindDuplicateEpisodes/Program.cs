using System.Reflection;
using FindDuplicateEpisodes;
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
    .AddScoped<FindDuplicateEpisodesProcessor>()
    .BindConfiguration<CosmosDbSettings>("cosmosdbv2");

using var host = builder.Build();
var processor = host.Services.GetService<FindDuplicateEpisodesProcessor>()!;
await processor.Run();
