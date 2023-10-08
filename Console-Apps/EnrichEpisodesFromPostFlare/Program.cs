using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.EnrichEpisodesFromPostFlare;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
    .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create("reddit-posts"))
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        MaxDepth = 0,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = true

    })
    .AddScoped<SubredditPostFlareEnricher>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
RedditClientFactory.AddRedditClient(builder.Services);


builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<RedditSettings>().Bind(builder.Configuration.GetSection("reddit"));
builder.Services
    .AddOptions<SubredditSettings>().Bind(builder.Configuration.GetSection("subreddit"));


using var host = builder.Build();
var processor = host.Services.GetService<SubredditPostFlareEnricher>();
await processor!.Run(false);