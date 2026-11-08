using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class TvShowRepository(
    Container tvShowsContainer,
    ILogger<TvShowRepository> logger,
    IFileKeyClaimStore fileKeyClaims)
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
        await fileKeyClaims.Acknowledge(tvShow.FileKey);
    }

    public Task<bool> PatchIndexerState(Guid tvShowId, CatalogueParentIndexerPatch patch) =>
        CatalogueParentIndexerPatchOperations.Apply(tvShowsContainer, tvShowId, patch);

    public Task<bool> RaiseLatestReleased(Guid tvShowId, DateTime latestReleased) =>
        CatalogueParentIndexerPatchOperations.RaiseLatestReleased(tvShowsContainer, tvShowId, latestReleased);

    public async Task Delete(Guid tvShowId)
    {
        try
        {
            await tvShowsContainer.DeleteItemAsync<TvShow>(
                tvShowId.ToString(),
                new PartitionKey(tvShowId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }

    public Task<int> Count()
    {
        var iterator = tvShowsContainer.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(ex, "{method}: error counting tv-shows.", nameof(Count)));
    }

    public IAsyncEnumerable<TvShow> GetAll()
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving tv-shows.", nameof(GetAll)));
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

    public IAsyncEnumerable<TvShow> GetAllBy(Expression<Func<TvShow, bool>> selector)
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving tv-shows with selector.", nameof(GetAllBy)));
    }

    public IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TvShow, bool>> selector,
        Expression<Func<TvShow, TProjection>> projection)
    {
        var query = tvShowsContainer
            .GetItemLinqQueryable<TvShow>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving projected tv-shows.", nameof(GetAllBy)));
    }
}
