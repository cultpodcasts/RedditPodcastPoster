using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastsUpdater(
    IPodcastUpdater podcastUpdater,
    IPodcastRepository podcastRepository,
    IFlushable flushableCaches,
    ILogger<PodcastsUpdater> logger)
    : IPodcastsUpdater
{
    public async Task<bool> UpdatePodcasts(IndexingContext indexingContext)
    {
        var success = true;
        logger.LogInformation($"{nameof(UpdatePodcasts)} Retrieving podcasts.");
        var podcastIds = podcastRepository.GetAllBy(
            podcast => (
                           (!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                           podcast.IndexAllEpisodes) ||
                       podcast.EpisodeIncludeTitleRegex != "",
            x => x.Id);
        logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing Starting.");
        await foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null &&
                (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))
            {
                try
                {
                    var result = await podcastUpdater.Update(podcast, indexingContext);
                    var resultReport = result.ToString();
                    if (!result.Success)
                    {
                        logger.LogError(resultReport);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(resultReport))
                        {
                            logger.LogInformation(result.ToString());
                        }
                    }

                    success &= result.Success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure updating podcast with id '{podcastId}' and name '{podcastName}'.",
                        podcast.Id, podcast.Name);
                    success = false;
                }
                finally
                {
                    flushableCaches.Flush();
                }
            }
        }

        logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing complete.");
        return success;
    }
}