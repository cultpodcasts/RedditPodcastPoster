using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnrichEpisodesFromPostFlare;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subreddit.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddFileRepository()
    .AddRepositories(builder.Configuration)
    .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        MaxDepth = 0,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = true
    })
    .AddSingleton<SubredditPostFlareEnricher>()
    .AddSubredditServices(builder.Configuration);

using var host = builder.Build();
var processor = host.Services.GetService<SubredditPostFlareEnricher>();
await processor!.Run(false);