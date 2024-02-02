using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using System.Threading.Tasks;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastsUpdater(
    IPodcastUpdaterFactory podcastUpdaterFactory,
    IPodcastRepository podcastRepository,
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

        var throttler = new SemaphoreSlim(5);
        var indexTasks = podcastIds.Select(async podcastId =>
        {
            await throttler.WaitAsync();
            try
            {
                return await IndexPodcast(indexingContext, podcastId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Failure indexing podcast with podcast-id '{podcastId}'.");
            }
            finally
            {
                throttler.Release();
            }

            return false;
        });
        await Task.WhenAll(indexTasks);

        logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing complete.");
        return success;
    }

    private async Task<bool> IndexPodcast(IndexingContext indexingContext, Guid podcastId)
    {
        var success = false;
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast != null)
        {
            try
            {
                var podcastUpdater = podcastUpdaterFactory.Create();
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

                success = result.Success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failure updating podcast with id '{podcast.Id}' and name '{podcast.Name}'.");
                success = false;
            }
        }

        return success;
    }
}