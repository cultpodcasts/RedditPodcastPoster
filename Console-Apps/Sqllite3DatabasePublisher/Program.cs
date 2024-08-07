using System.Data.SQLite;
using System.Reflection;
using CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
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
    .AddRepositories()
    .AddEliminationTerms()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddSingleton<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>();


return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Request request)
{
    var databaseFileName = request.DatabaseName;
    var connectionString = $"Data Source={databaseFileName}";
    File.Delete(databaseFileName);
    SQLiteConnection.CreateFile(databaseFileName);
    builder.Services.AddDbContext<DatabaseContext>(options => options.UseSqlite(connectionString));
    using var host = builder.Build();
    var processor = host.Services.GetService<Sqllite3DatabasePublisher.Sqllite3DatabasePublisher>();
    await processor!.Run(request);
    return 0;
}



