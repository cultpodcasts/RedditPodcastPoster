using System.Reflection;
using System.Text.Json;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
    .AddScoped<IPodcastUpdater, PodcastUpdater>()
    .AddScoped<IEpisodeProvider, EpisodeProvider>()
    .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
    .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
    .AddScoped<IPodcastProcessor, PodcastProcessor>()
    .AddScoped<IFilenameSelector, FilenameSelector>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
    .AddScoped<ISpotifyUrlResolver, SpotifyUrlResolver>()
    .AddScoped<IAppleUrlResolver, AppleUrlResolver>()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
    .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddScoped<IEpisodeResolver, EpisodeResolver>()
    .AddSingleton<ITextSanitiser, TextSanitiser>()
    .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
    .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
    .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
    .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
    .AddScoped<ISpotifyIdResolver, SpotifyIdResolver>()
    .AddScoped<ISpotifyItemResolver, SpotifyItemResolver>()
    .AddScoped<ISpotifySearcher, SpotifySearcher>()
    .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
    .AddScoped<IEpisodePostManager, EpisodePostManager>()
    .AddScoped<IResolvedPodcastEpisodeAdaptor, ResolvedPodcastEpisodeAdaptor>()
    .AddScoped<IResolvedPodcastEpisodePoster, ResolvedPodcastEpisodePoster>()
    .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
    .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
    .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
    .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
    .AddScoped<IPodcastFilter, PodcastFilter>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddHttpClient<RemoteClient>();

SpotifyClientFactory.AddSpotifyClient(builder.Services);
RedditClientFactory.AddRedditClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);
CosmosDbClientFactory.AddCosmosClient(builder.Services);

builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));
builder.Services
    .AddOptions<RedditSettings>().Bind(builder.Configuration.GetSection("reddit"));
builder.Services
    .AddOptions<SubredditSettings>().Bind(builder.Configuration.GetSection("subreddit"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<PostingCriteria>().Bind(builder.Configuration.GetSection("postingCriteria"));

using var host = builder.Build();

return await Parser.Default.ParseArguments<ProcessRequest>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(ProcessRequest request)
{
    var podcastProcessor = host.Services.GetService<IPodcastProcessor>()!;
    var result = await podcastProcessor.Process(request);
    Console.WriteLine(result.ToString());
    return result.ToResultCode();
}