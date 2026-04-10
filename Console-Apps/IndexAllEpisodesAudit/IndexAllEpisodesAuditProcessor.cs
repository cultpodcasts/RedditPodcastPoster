using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace IndexAllEpisodesAudit;

public class IndexAllEpisodesAuditProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<IndexAllEpisodesAuditProcessor> logger)
{
    public async Task Process(IndexAllEpisodesAuditRequest request)
    {
        var indexAllEpisodePodcasts = await podcastRepository.GetAllBy(x => x.IndexAllEpisodes).ToListAsync();
        foreach (var podcast in indexAllEpisodePodcasts)
        {
            var mostRecentEpisode = await episodeRepository.GetMostRecentByPodcastId(podcast.Id);

            if (mostRecentEpisode != null)
            {
                var since = DateTime.UtcNow - mostRecentEpisode.Release;
                if (since > request.Since)
                {
                    logger.LogInformation(
                        "Podcast '{PodcastName}' with id '{PodcastId}'. Most recent episode-release {DateTime:d}",
                        podcast.Name, podcast.Id, mostRecentEpisode.Release);
                    if (!request.DryRun)
                    {
                        podcast.IndexAllEpisodes = false;
                        await podcastRepository.Save(podcast);
                    }
                }
            }
        }
    }
}