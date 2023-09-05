using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Persistence;
using SeedEliminationTerms;

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
    .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<EliminationTermsSeeder>()
    .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<EliminationTermsSeeder>();
await processor!.Run();