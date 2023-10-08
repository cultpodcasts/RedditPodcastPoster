using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.CosmosDbUploader;

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
    .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create("podcasts"))
    .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<CosmosDbUploader>()
    .AddSingleton<IPartitionKeySelector, PartitionKeySelector>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<CosmosDbUploader>();
await processor!.Run();