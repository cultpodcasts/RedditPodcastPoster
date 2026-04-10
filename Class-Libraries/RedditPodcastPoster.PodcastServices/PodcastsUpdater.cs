using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastsUpdater(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    IPodcastUpdater podcastUpdater,
    IPodcastRepository podcastRepository,
    IFlushable flushableCaches,
    ILogger<PodcastsUpdater> logger)
    : IPodcastsUpdater
{
    public async Task<bool> UpdatePodcasts(Guid[] podcastIds, IndexingContext indexingContext)
    {
        var success = true;
        logger.LogInformation("{nameofUpdatePodcasts} Indexing Starting.", nameof(UpdatePodcasts));
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            var performAutoIndex = podcast != null &&
                                   (podcast.IndexAllEpisodes ||
                                    !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex));
            if (performAutoIndex)
            {
                try
                {
                    var result = await podcastUpdater.Update(podcast!, false, indexingContext);
                    var resultReport = result.ToString();
                    if (!result.Success)
                    {
                        logger.LogError("{report}",resultReport);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(resultReport))
                        {
                            logger.LogInformation("{result}",result.ToString());
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

        logger.LogInformation("{nameofUpdatePodcasts} Indexing complete.", nameof(UpdatePodcasts));
        return success;
    }

    public async Task<bool> UpdatePodcasts(IndexingContext indexingContext)
    {
        var podcastIds = indexablePodcastIdProvider.GetIndexablePodcastIds();
        return await UpdatePodcasts(await podcastIds.ToArrayAsync(), indexingContext);
    }
}