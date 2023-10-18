using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Text.KnownTerms;
using SeedKnownTerms;

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
    .AddScoped<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<KnownTermsSeeder>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<KnownTermsSeeder>();
await processor!.Run();