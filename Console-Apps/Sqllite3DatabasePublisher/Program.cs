using System.Data.SQLite;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence;
using Sqllite3DatabasePublisher;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
    .AddScoped<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>();

var databaseFileName = "podcasts.sqlite";
var connectionString = $"Data Source={databaseFileName}";

File.Delete(databaseFileName);
SQLiteConnection.CreateFile(databaseFileName);
builder.Services.AddDbContext<PodcastContext>(options => options.UseSqlite(connectionString));

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>();
await processor!.Run();