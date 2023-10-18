using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.UrlSubmission;
using RedditPodcastPoster.Matching;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Subreddit;
using TextClassifierTraining;

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
    .AddScoped<UrlSubmitter>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<ISubredditPostProvider, SubredditPostProvider>()
    .AddScoped<ISubredditRepository, SubredditRepository>()
    .AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IRepositoryFactory, RepositoryFactory>()
    .AddScoped<TrainingDataProcessor>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
RedditClientFactory.AddRedditClient(builder.Services);
SpotifyClientFactory.AddSpotifyClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);


builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<RedditSettings>().Bind(builder.Configuration.GetSection("reddit"));
builder.Services
    .AddOptions<SubredditSettings>().Bind(builder.Configuration.GetSection("subreddit"));
builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));


using var host = builder.Build();
return await Parser.Default.ParseArguments<TrainingDataRequest>(args)
    .MapResult(async trainingDataRequest => await Run(trainingDataRequest),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(TrainingDataRequest request)
{
    var processor = host.Services.GetService<TrainingDataProcessor>()!;
    await processor.Process(request);
    return 0;
}