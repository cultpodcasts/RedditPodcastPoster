using System.Reflection;
using CultPodcasts.DatabasePublisher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;

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
    .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create())
    .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<PublicDatabasePublisher>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<PublicDatabasePublisher>();
await processor!.Run();