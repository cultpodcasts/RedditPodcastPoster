using System.Data.SQLite;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Text.Extensions;
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
    .AddRepositories(builder.Configuration)
    .AddEliminationTerms()
    .AddSingleton<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>();

var databaseFileName = "podcasts.sqlite";
var connectionString = $"Data Source={databaseFileName}";

File.Delete(databaseFileName);
SQLiteConnection.CreateFile(databaseFileName);
builder.Services.AddDbContext<PodcastContext>(options => options.UseSqlite(connectionString));

using var host = builder.Build();
var processor = host.Services.GetService<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>();
await processor!.Run();