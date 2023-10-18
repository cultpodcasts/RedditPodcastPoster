using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Matching;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.Text.KnownTerms;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.UrlSubmission;
using Tweet;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<UrlSubmitter>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<TweetProcessor>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddSingleton<ITweetBuilder, TweetBuilder>()
    .AddScoped<ITwitterClient, TwitterClient>()
    .AddScoped<ITextSanitiser, TextSanitiser>()
    .AddScoped<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
    .AddScoped<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton(s => s.GetService<IKnownTermsProviderFactory>()!.Create().GetAwaiter().GetResult());

CosmosDbClientFactory.AddCosmosClient(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));

builder.Services
    .AddOptions<TwitterOptions>().Bind(builder.Configuration.GetSection("twitter"));


using var host = builder.Build();
return await Parser.Default.ParseArguments<TweetRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(TweetRequest request)
{
    var tweetProcessor = host.Services.GetService<TweetProcessor>()!;
    await tweetProcessor.Run(request);
    return 0;
}