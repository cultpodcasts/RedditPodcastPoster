using System.Reflection;
using IndexPodcast;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
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
    .AddSingleton<IndexIndividualPodcastProcessor>()
    .AddRepositories(builder.Configuration)
    .AddSingleton<PodcastFactory>()
    .AddPodcastServices(builder.Configuration)
    .AddAppleServices()
    .AddYouTubeServices(builder.Configuration)
    .AddSpotifyServices(builder.Configuration)
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddEliminationTerms()
    .AddHttpClient();
using var host = builder.Build();

var podcastProcessor = host.Services.GetService<IndexIndividualPodcastProcessor>()!;
var baseline = DateTime.Today.AddDays(-1 * int.Parse(args[1]));
await podcastProcessor.Run(Guid.Parse(args[0]), new IndexingContext(baseline) {SkipPodcastDiscovery = false});
return 0;