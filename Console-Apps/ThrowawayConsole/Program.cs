using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories();

using var host = builder.Build();

var episodeRepository = host.Services.GetRequiredService<IEpisodeRepository>();
var podcastRepository = host.Services.GetRequiredService<IPodcastRepositoryV2>();

var fourWeeksAgo = DateTime.UtcNow.AddDays(-28);
var latestByPodcast = new Dictionary<Guid, DateTime>();
var recentEpisodeCount = 0;

await foreach (var episode in episodeRepository.GetAllBy(
                   x => x.Release >= fourWeeksAgo,
                   x => new EpisodeReleaseProjection
                   {
                       PodcastId = x.PodcastId,
                       Release = x.Release
                   }))
{
    recentEpisodeCount++;

    if (!latestByPodcast.TryGetValue(episode.PodcastId, out var latestRelease) ||
        episode.Release > latestRelease)
    {
        latestByPodcast[episode.PodcastId] = episode.Release;
    }
}

var updatedPodcasts = 0;
var missingPodcastIds = new List<Guid>();

foreach (var (podcastId, latestRelease) in latestByPodcast)
{
    var podcast = await podcastRepository.GetPodcast(podcastId);
    if (podcast == null)
    {
        missingPodcastIds.Add(podcastId);
        continue;
    }

    if (podcast.LatestReleased == latestRelease)
    {
        continue;
    }

    podcast.LatestReleased = latestRelease;
    await podcastRepository.Save(podcast);
    updatedPodcasts++;
}

Console.WriteLine(
    "LatestReleased backfill completed. SinceUtc: {0:O}, RecentEpisodes: {1}, PodcastsWithRecentEpisodes: {2}, UpdatedPodcasts: {3}, MissingPodcasts: {4}",
    fourWeeksAgo,
    recentEpisodeCount,
    latestByPodcast.Count,
    updatedPodcasts,
    missingPodcastIds.Count);

if (missingPodcastIds.Count > 0)
{
    Console.WriteLine(
        "Missing podcast ids: {0}",
        string.Join(",", missingPodcastIds));
}

public sealed class EpisodeReleaseProjection
{
    public Guid PodcastId { get; init; }
    public DateTime Release { get; init; }
}