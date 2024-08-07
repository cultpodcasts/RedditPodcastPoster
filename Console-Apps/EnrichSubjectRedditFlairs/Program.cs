using System.Reflection;
using EnrichSubjectRedditFlairs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Subreddit.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddFileRepository()
    .AddRepositories()
    .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
    .AddSingleton<RedditFlairsProcessor>()
    .AddRedditServices(builder.Configuration)
    .AddSubredditServices(builder.Configuration)
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddScoped<ISubjectCleanser, SubjectCleanser>();

using var host = builder.Build();
var processor = host.Services.GetService<RedditFlairsProcessor>();
await processor!.Run();