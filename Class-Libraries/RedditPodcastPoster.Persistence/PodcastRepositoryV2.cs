using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;
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
}
