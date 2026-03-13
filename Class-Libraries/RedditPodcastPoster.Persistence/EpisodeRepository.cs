using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EpisodeRepository(
    Container container,
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
        if (episode.PodcastId==Guid.Empty)
        {
            throw new InvalidOperationException("Episode.PodcastId must be set before saving.");
        }

        await container.UpsertItemAsync(episode, ToPartitionKey(episode.PodcastId));
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
        try
        {
            await container.DeleteItemAsync<Episode>(episodeId.ToString(), ToPartitionKey(podcastId));
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
}
