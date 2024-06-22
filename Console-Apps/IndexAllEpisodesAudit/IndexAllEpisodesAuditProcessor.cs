using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace IndexAllEpisodesAudit;

public class IndexAllEpisodesAuditProcessor(
    IPodcastRepository podcastRepository,
    ILogger<IndexAllEpisodesAuditProcessor> logger)
{
    public async Task Process(IndexAllEpisodesAuditRequest request)
    {
        var indexAllEpisodePodcasts = await podcastRepository.GetAllBy(x => x.IndexAllEpisodes == true).ToListAsync();
        foreach (var podcast in indexAllEpisodePodcasts)
        {
            var mostRecentEpisode = podcast.Episodes.MaxBy(x => x.Release);
            var since = DateTime.UtcNow - mostRecentEpisode.Release;
            if (since > request.Since)
            {
                logger.LogInformation(
                    $"Podcast '{podcast.Name}' with id '{podcast.Id}'. Most recent episode-release {mostRecentEpisode.Release:d}");
                if (!request.DryRun)
                {
                    podcast.IndexAllEpisodes = false;
                    await podcastRepository.Save(podcast);
                }
            }
        }
    }
}