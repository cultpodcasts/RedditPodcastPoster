using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class TvShowEpisodeRepository(
    Container container,
    ILogger<TvShowEpisodeRepository> logger)
    : ITvShowEpisodeRepository
{
    private static PartitionKey ToPartitionKey(Guid tvShowId) => new(tvShowId.ToString());

    public async Task<TvShowEpisode?> GetTvShowEpisode(Guid tvShowId, Guid episodeId)
    {
        try
        {
            return await container.ReadItemAsync<TvShowEpisode>(episodeId.ToString(), ToPartitionKey(tvShowId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public IAsyncEnumerable<TvShowEpisode> GetAll() => GetAllBy(_ => true);

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
                logger.LogError(ex, "{method}: error counting tv-show episodes.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async Task<int> Count(Guid tvShowId)
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.tvShowId = @tvShowId")
                .WithParameter("@tvShowId", tvShowId.ToString()),
            requestOptions: new QueryRequestOptions { PartitionKey = ToPartitionKey(tvShowId) });

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
                logger.LogError(ex, "{method}: error counting tv-show episodes by show-id '{TvShowId}'.",
                    nameof(Count), tvShowId);
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(tvShowId)
            })
            .Where(x => x.TvShowId == tvShowId);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<TvShowEpisode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving tv-show episodes.", nameof(GetByTvShowId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TvShowEpisode> GetByTvShowId(
        Guid tvShowId,
        Expression<Func<TvShowEpisode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(tvShowId)
            })
            .Where(x => x.TvShowId == tvShowId)
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<TvShowEpisode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving filtered tv-show episodes.", nameof(GetByTvShowId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task Save(TvShowEpisode episode)
    {
        if (episode.Id == Guid.Empty || episode.TvShowId == Guid.Empty)
        {
            throw new ArgumentException("TvShowEpisode Id and TvShowId must be set before save.");
        }

        await container.UpsertItemAsync(episode, ToPartitionKey(episode.TvShowId));
    }

    public async Task Save(IEnumerable<TvShowEpisode> episodes)
    {
        foreach (var episode in episodes)
        {
            await Save(episode);
        }
    }

    public async Task Delete(Guid tvShowId, Guid episodeId)
    {
        await container.DeleteItemAsync<TvShowEpisode>(episodeId.ToString(), ToPartitionKey(tvShowId));
    }

    public async Task<TvShowEpisode?> GetBy(Expression<Func<TvShowEpisode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions())
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

    public async IAsyncEnumerable<TvShowEpisode> GetAllBy(Expression<Func<TvShowEpisode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TvShowEpisode> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving tv-show episodes with selector.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TvShowEpisode, bool>> selector,
        Expression<Func<TvShowEpisode, TProjection>> projection)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions())
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
                logger.LogError(ex, "{method}: error retrieving projected tv-show episodes.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
