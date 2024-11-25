using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Subreddit.Extensions;
using TextClassifierTraining;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddFileRepository("subreddit-repository")
    .AddRepositories()
    .AddSubredditServices(builder.Configuration)
    .AddSpotifyServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddSingleton<TrainingDataProcessor>()
    .AddSingleton<ISubjectCleanser, SubjectCleanser>()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddRedditServices();

using var host = builder.Build();
return await Parser.Default.ParseArguments<TrainingDataRequest>(args)
    .MapResult(async trainingDataRequest => await Run(trainingDataRequest),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(TrainingDataRequest request)
{
    var processor = host.Services.GetService<TrainingDataProcessor>()!;
    await processor.Process(request);
    return 0;
}