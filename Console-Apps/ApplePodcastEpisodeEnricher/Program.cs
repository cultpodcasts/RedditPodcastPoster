using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ApplePodcastEpisodeEnricher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
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
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<MissingFilesProcessor>()
    .AddScoped<IApplePodcastService, ApplePodcastService>()
    .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
    .AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
    {
        var appleBearerTokenProvider = services.GetService<IAppleBearerTokenProvider>();
        httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        httpClient.DefaultRequestHeaders.Authorization = appleBearerTokenProvider!.GetHeader().GetAwaiter().GetResult();
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
    });


CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));

using var host = builder.Build();
var processor = host.Services.GetService<MissingFilesProcessor>();
await processor!.Run();