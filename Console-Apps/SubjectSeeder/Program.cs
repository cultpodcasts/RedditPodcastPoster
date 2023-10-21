using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Extensions;
using SubjectSeeder;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    //.AddSingleton<IFileRepositoryFactory, FileRepositoryFactory>()
    //.AddScoped(s => (IDataRepository) s.GetService<IFileRepositoryFactory>().Create())
    //.AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddRepositories(builder.Configuration)
    .AddRepository<Subject>()
    .AddSingleton<SubjectsSeeder>();


using var host = builder.Build();
var processor = host.Services.GetService<SubjectsSeeder>();
await processor!.Run();