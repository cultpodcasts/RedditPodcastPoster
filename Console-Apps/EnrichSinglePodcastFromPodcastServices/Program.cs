using System.Reflection;
using EnrichSinglePodcastFromPodcastServices;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
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
    .AddScoped<EnrichSinglePodcastFromPodcastServicesProcessor>()
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

builder.Services.AddPostingCriteria(builder.Configuration);

using var host = builder.Build();

var podcastProcessor = host.Services.GetService<EnrichSinglePodcastFromPodcastServicesProcessor>()!;
if (Guid.TryParse(args[0], out var podcastId))
{
    await podcastProcessor.Run(podcastId);
}
else
{
    throw new ArgumentException($"Could not parse guid '{args[0]}'.");
}

return 0;