using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

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
        var podcastIds = await podcastRepository.GetAllBy(podcast =>
            podcast.IndexAllEpisodes ||
            (podcast.EpisodeIncludeTitleRegex != null && podcast.EpisodeIncludeTitleRegex != ""), x => x.Id);
        logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing Starting.");
        foreach (var podcastId in podcastIds)
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
                    logger.LogError(ex, $"Failure updating podcast with id '{podcast.Id}' and name '{podcast.Name}'.");
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