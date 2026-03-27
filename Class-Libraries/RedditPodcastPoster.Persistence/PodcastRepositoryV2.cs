using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Podcast = RedditPodcastPoster.Models.Podcast;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class PodcastRepositoryV2(
    Container podcastsContainer,
    ILogger<PodcastRepositoryV2> logger)
    : IPodcastRepositoryV2
{
    public async Task<Podcast?> GetPodcast(Guid podcastId)
    {
        try
        {
            return await podcastsContainer.ReadItemAsync<Podcast>(podcastId.ToString(), new PartitionKey(podcastId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Save(Podcast podcast)
    {
        await podcastsContainer.UpsertItemAsync(podcast, new PartitionKey(podcast.Id.ToString()));
    }

    public async Task<int> Count()
    {
        var iterator = podcastsContainer.GetItemQueryIterator<int>(
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
                logger.LogError(ex, "{method}: error counting podcasts.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<Podcast> GetAll()
    {
        var query = podcastsContainer
            .GetItemLinqQueryable<Podcast>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Podcast> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving podcasts.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector)
    {
        var query = podcastsContainer
            .GetItemLinqQueryable<Podcast>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector)
    {
        var query = podcastsContainer
            .GetItemLinqQueryable<Podcast>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Podcast> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving podcasts with selector.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Podcast, bool>> selector,
        Expression<Func<Podcast, TProjection>> projection)
    {
        var query = podcastsContainer
            .GetItemLinqQueryable<Podcast>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TProjection> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving projected podcasts.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
