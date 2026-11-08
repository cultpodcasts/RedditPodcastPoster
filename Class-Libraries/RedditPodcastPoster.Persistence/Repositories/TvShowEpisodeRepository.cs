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

    public Task<ETagged<TvShowEpisode>?> GetTvShowEpisodeWithETag(Guid tvShowId, Guid episodeId) =>
        CataloguePlayableFieldPatchOperations.ReadWithETag<TvShowEpisode>(
            container, episodeId.ToString(), ToPartitionKey(tvShowId));

    public Task<CataloguePlayablePatchOutcome> PatchFields(
        Guid tvShowId,
        Guid episodeId,
        string ifMatchETag,
        CataloguePlayableFieldPatch patch) =>
        CataloguePlayableFieldPatchOperations.Apply(
            container, episodeId.ToString(), ToPartitionKey(tvShowId), ifMatchETag, patch);

    public IAsyncEnumerable<TvShowEpisode> GetAll() => GetAllBy(_ => true);

    public Task<int> Count()
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(ex, "{method}: error counting tv-show episodes.", nameof(Count)));
    }

    public Task<int> Count(Guid tvShowId)
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.tvShowId = @tvShowId")
                .WithParameter("@tvShowId", tvShowId.ToString()),
            requestOptions: new QueryRequestOptions { PartitionKey = ToPartitionKey(tvShowId) });

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(
                ex,
                "{method}: error counting tv-show episodes by show-id '{TvShowId}'.",
                nameof(Count),
                tvShowId));
    }

    public IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(tvShowId)
            })
            .Where(x => x.TvShowId == tvShowId);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving tv-show episodes.", nameof(GetByTvShowId)));
    }

    public IAsyncEnumerable<TvShowEpisode> GetByTvShowId(
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

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving filtered tv-show episodes.", nameof(GetByTvShowId)));
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

    public IAsyncEnumerable<TvShowEpisode> GetAllBy(Expression<Func<TvShowEpisode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving tv-show episodes with selector.", nameof(GetAllBy)));
    }

    public IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<TvShowEpisode, bool>> selector,
        Expression<Func<TvShowEpisode, TProjection>> projection)
    {
        var query = container
            .GetItemLinqQueryable<TvShowEpisode>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving projected tv-show episodes.", nameof(GetAllBy)));
    }
}
