using System.Diagnostics;
using System.Reflection;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSubjectServices()
    .AddSubjectProvider()
    .AddTextSanitiser()
    .AddCommonServices()
    .AddPostingCriteria();

using var host = builder.Build();

var podcastRepository = host.Services.GetRequiredService<IPodcastRepositoryV2>();
var episodeRepository = host.Services.GetRequiredService<IEpisodeRepository>();

Console.WriteLine("🔧 Bulk-fixing podcasts' LatestReleased based on recent episodes...\n");

var since = DateTime.UtcNow.AddDays(-7);

// Find episodes released in the past week
var recentEpisodes = await episodeRepository
    .GetAllBy(x => x.Release >= since && !x.Removed)
    .ToArrayAsync();

Console.WriteLine($"📺 Found {recentEpisodes.Length} episodes released in the past week\n");

// Group by podcast ID and order by release descending
var groupedByPodcast = recentEpisodes
    .GroupBy(x => x.PodcastId)
    .OrderByDescending(g => g.Max(x => x.Release))
    .ToArray();

Console.WriteLine($"📻 Found {groupedByPodcast.Length} podcasts with recent episodes\n");

var updated = 0;

foreach (var podcastGroup in groupedByPodcast)
{
    var podcastId = podcastGroup.Key;
    var mostRecentEpisode = podcastGroup.OrderByDescending(x => x.Release).First();
    
    var podcast = await podcastRepository.GetPodcast(podcastId);
    if (podcast == null)
    {
        Console.WriteLine($"⚠️  Podcast with id {podcastId} not found\n");
        continue;
    }

    // Check if LatestReleased needs updating
    if (podcast.LatestReleased == null || podcast.LatestReleased < mostRecentEpisode.Release)
    {
        var oldValue = podcast.LatestReleased;
        podcast.LatestReleased = mostRecentEpisode.Release;
        await podcastRepository.Save(podcast);
        updated++;

        Console.WriteLine($"✅ {podcast.Name}");
        Console.WriteLine($"   Old LatestReleased: {oldValue:O}");
        Console.WriteLine($"   New LatestReleased: {mostRecentEpisode.Release:O}");
        Console.WriteLine($"   Episode: {mostRecentEpisode.Title}\n");
    }
}

Console.WriteLine($"🎉 Updated {updated} podcasts");

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}