using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EpisodeRepository(
    Container container,
    ILookupRepository lookupRepository,
    IPodcastRepositoryV2 podcastRepository,
    ILogger<EpisodeRepository> logger)
    : IEpisodeRepository
{
    private static PartitionKey ToPartitionKey(Guid podcastId) => new(podcastId.ToString());

    public async Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId)
    {
        try
        {
            return await container.ReadItemAsync<Episode>(episodeId.ToString(), ToPartitionKey(podcastId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public IAsyncEnumerable<Episode> GetAll()
    {
        return GetAllBy(_ => true);
    }

    public async Task<int> Count()
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        while (iterator.HasMoreResults)
        {
            try
            {
                foreach (var count in await iterator.ReadNextAsync())
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error counting episodes.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(podcastId)
            })
            .Where(x => x.PodcastId == podcastId);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<Episode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving episodes by podcast-id.", nameof(GetByPodcastId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId, Expression<Func<Episode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(podcastId)
            })
            .Where(x => x.PodcastId == podcastId)
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<Episode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving episodes by podcast-id with additional filter.", nameof(GetByPodcastId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task<Episode?> GetMostRecentByPodcastId(Guid podcastId)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(podcastId)
            })
            .Where(x => x.PodcastId == podcastId)
            .OrderByDescending(x => x.Release)
            .Take(1);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            foreach (var item in await items.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async Task Save(Episode episode)
    {
        if (episode.PodcastId == Guid.Empty)
        {
            throw new InvalidOperationException("Episode.PodcastId must be set before saving.");
        }

        var existingEpisode = await GetEpisode(episode.PodcastId, episode.Id);
        var previousCountedState = existingEpisode is not null && IsCountedForHomepage(existingEpisode);
        var nextCountedState = IsCountedForHomepage(episode);

        await container.UpsertItemAsync(episode, ToPartitionKey(episode.PodcastId));
        await UpdateHomePageActiveEpisodeCount(previousCountedState, nextCountedState);
        await UpdatePodcastLatestReleasedOnSave(episode, existingEpisode);
    }

    public async Task Save(IEnumerable<Episode> episodes)
    {
        foreach (var episode in episodes)
        {
            await Save(episode);
        }
    }

    public async Task Delete(Guid podcastId, Guid episodeId)
    {
        var existingEpisode = await GetEpisode(podcastId, episodeId);
        try
        {
            await container.DeleteItemAsync<Episode>(episodeId.ToString(), ToPartitionKey(podcastId));
            if (existingEpisode is not null && IsCountedForHomepage(existingEpisode))
            {
                await lookupRepository.IncrementHomePageActiveEpisodeCount(-1);
            }

            await UpdatePodcastLatestReleasedOnDelete(podcastId, existingEpisode);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }

    public async Task<Episode?> GetBy(Expression<Func<Episode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            foreach (var item in await items.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async IAsyncEnumerable<Episode> GetAllBy(Expression<Func<Episode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<Episode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving episodes.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Episode, bool>> selector,
        Expression<Func<Episode, TProjection>> projection)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<TProjection> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving projected episodes.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    private static bool IsCountedForHomepage(Episode episode)
    {
        return !episode.Removed && episode.PodcastRemoved != true;
    }

    private async Task UpdateHomePageActiveEpisodeCount(bool previousCountedState, bool nextCountedState)
    {
        if (previousCountedState == nextCountedState)
        {
            return;
        }

        await lookupRepository.IncrementHomePageActiveEpisodeCount(nextCountedState ? 1 : -1);
    }

    private async Task UpdatePodcastLatestReleasedOnSave(Episode episode, Episode? existingEpisode)
    {
        var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
        if (podcast == null)
        {
            return;
        }

        if (podcast.LatestReleased == null || episode.Release > podcast.LatestReleased.Value)
        {
            podcast.LatestReleased = episode.Release;
            await podcastRepository.Save(podcast);
            return;
        }

        if (existingEpisode != null &&
            podcast.LatestReleased != null &&
            existingEpisode.Release >= podcast.LatestReleased.Value &&
            episode.Release < existingEpisode.Release)
        {
            await RecomputePodcastLatestReleased(podcast, episode.PodcastId);
        }
    }

    private async Task UpdatePodcastLatestReleasedOnDelete(Guid podcastId, Episode? deletedEpisode)
    {
        if (deletedEpisode == null)
        {
            return;
        }

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast?.LatestReleased == null || deletedEpisode.Release < podcast.LatestReleased.Value)
        {
            return;
        }

        await RecomputePodcastLatestReleased(podcast, podcastId);
    }

    private async Task RecomputePodcastLatestReleased(Podcast podcast, Guid podcastId)
    {
        var mostRecentEpisode = await GetMostRecentByPodcastId(podcastId);
        var recomputedLatestReleased = mostRecentEpisode?.Release;
        if (podcast.LatestReleased == recomputedLatestReleased)
        {
            return;
        }

        podcast.LatestReleased = recomputedLatestReleased;
        await podcastRepository.Save(podcast);
    }
}
