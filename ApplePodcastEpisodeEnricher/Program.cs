using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using ApplePodcastEpisodeEnricher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;

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
    .AddSingleton<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddScoped<MissingFilesProcessor>()
    .AddScoped<IApplePodcastService, ApplePodcastService>()
    .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
    .AddHttpClient<IApplePodcastService, ApplePodcastService>(c =>
    {
        c.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(args[0], args[1]);
        c.DefaultRequestHeaders.Accept.Clear();
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        c.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
        c.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
        c.DefaultRequestHeaders.UserAgent.Clear();
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
    });

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));

using var host = builder.Build();
var processor = host.Services.GetService<MissingFilesProcessor>();
await processor!.Run();