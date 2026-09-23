using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class TvShowRepository(
    Container tvShowsContainer,
    ILogger<TvShowRepository> logger)
    : ITvShowRepository
{
    public async Task<TvShow?> GetTvShow(Guid tvShowId)
    {
        try
        {
            return await tvShowsContainer.ReadItemAsync<TvShow>(
                tvShowId.ToString(),
                new PartitionKey(tvShowId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Save(TvShow tvShow)
    {
        await tvShowsContainer.UpsertItemAsync(tvShow, new PartitionKey(tvShow.Id.ToString()));
    }

    public async Task<int> Count()
    {
        var iterator = tvShowsContainer.GetItemQueryIterator<int>(
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
                logger.LogError(ex, "{method}: error counting tv-shows.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<TvShow> GetAll()
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TvShow> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving tv-shows.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task<TvShow?> GetBy(Expression<Func<TvShow, bool>> selector)
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
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

    public async IAsyncEnumerable<TvShow> GetAllBy(Expression<Func<TvShow, bool>> selector)
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TvShow> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving tv-shows with selector.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TvShow, bool>> selector,
        Expression<Func<TvShow, TProjection>> projection)
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
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
                logger.LogError(ex, "{method}: error retrieving projected tv-shows.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
