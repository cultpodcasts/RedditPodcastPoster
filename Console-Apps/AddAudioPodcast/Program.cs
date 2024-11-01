using System.Reflection;
using AddAudioPodcast;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<AddAudioPodcastProcessor>()
    .AddRepositories()
    .AddCommonServices(builder.Configuration)
    .AddPodcastServices()
    .AddAppleServices()
    .AddSpotifyServices(builder.Configuration)
    .AddYouTubeServices(builder.Configuration)
    .AddEliminationTerms()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddTextSanitiser()
    .AddHttpClient();

builder.Services.AddPostingCriteria(builder.Configuration);
builder.Services.AddDelayedYouTubePublication(builder.Configuration);

using var host = builder.Build();


return await Parser.Default.ParseArguments<AddAudioPodcastRequest>(args)
    .MapResult(async addAudioPodcastRequest => await Run(addAudioPodcastRequest),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(AddAudioPodcastRequest request)
{
    var podcastProcessor = host.Services.GetService<AddAudioPodcastProcessor>()!;
    await podcastProcessor.Create(request);
    return 0;
}