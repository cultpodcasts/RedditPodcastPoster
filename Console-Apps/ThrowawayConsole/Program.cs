using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;
using System.Diagnostics;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddContentPublishing()
    .AddCloudflareClients()
    .AddTextSanitiser()
    .AddPodcastServices()
    .AddSubjectServices()
    .AddRedditServices()
    .AddSpotifyServices()
    .AddUrlSubmission()
    .AddBBCServices()
    .AddInternetArchiveServices()
    .AddAppleServices()
    .AddSpotifyServices()
    .AddYouTubeServices(ApplicationUsage.Api)
    .AddScoped(s => new iTunesSearchManager())
    .AddSubjectServices()
    .AddSubjectProvider()
    .AddHttpClient();


using var host = builder.Build();
var subjectRepository= host.Services.GetRequiredService<ISubjectRepository>();
var subjects = await subjectRepository.GetAll().ToListAsync();

var podcastResult= new PodcastResult
{
    Subjects = new[]
    {
       "X", "_America"
    },
    PodcastName = "x",
    EpisodeTitle = "y",
    EpisodeDescription = "z"
};

var subjectKnownTerms = (podcastResult.Subjects ?? [])
    .Select(x => subjects.SingleOrDefault(y => y.Name == x))
    .SelectMany(x => x?.KnownTerms ?? []).ToArray();



return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}