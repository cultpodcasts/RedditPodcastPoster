using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class FilmRepository(
    Container filmsContainer,
    ILogger<FilmRepository> logger)
    : IFilmRepository
{
    public async Task<Film?> GetFilm(Guid filmId)
    {
        try
        {
            return await filmsContainer.ReadItemAsync<Film>(
                filmId.ToString(),
                new PartitionKey(filmId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Save(Film film)
    {
        // Create, or a full document that was not read-modify-written here. An existing film a caller has read
        // and changed goes through Update, so this upsert cannot drop a trailer attached since that read.
        await filmsContainer.UpsertItemAsync(film, new PartitionKey(film.Id.ToString()));
    }

    public Task<Film?> Update(Guid id, Func<Film, bool> mutate) =>
        FilmDocumentUpdate.Apply(id, mutate, GetFilmForUpdate, TryReplace);

    public async Task<FilmVersion?> GetFilmForUpdate(Guid filmId)
    {
        try
        {
            var response = await filmsContainer.ReadItemAsync<Film>(
                filmId.ToString(),
                new PartitionKey(filmId.ToString()));
            return new FilmVersion(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> TryReplace(Film film, string etag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);
        try
        {
            await filmsContainer.ReplaceItemAsync(
                film,
                film.Id.ToString(),
                new PartitionKey(film.Id.ToString()),
                new ItemRequestOptions { IfMatchEtag = etag });
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode is System.Net.HttpStatusCode.PreconditionFailed
                                             or System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                "{method}: film '{filmId}' changed or was deleted since it was read ({status}).",
                nameof(TryReplace), film.Id, ex.StatusCode);
            return false;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetFilmIdsByTrailerId(Guid trailerId)
    {
        var query = new QueryDefinition(
                "SELECT VALUE c.id FROM c WHERE EXISTS(SELECT VALUE t FROM t IN c.trailers WHERE t.id = @trailerId)")
            .WithParameter("@trailerId", trailerId.ToString());

        // Films are partitioned by film id. The trailer id is not that key, so the query must not set one.
        var iterator = filmsContainer.GetItemQueryIterator<string>(
            query,
            requestOptions: new QueryRequestOptions());

        var ids = new List<Guid>();
        await foreach (var id in CatalogueFeed.Read(
                           iterator,
                           ex => logger.LogError(
                               ex,
                               "{method}: error finding films holding trailer '{trailerId}'.",
                               nameof(GetFilmIdsByTrailerId),
                               trailerId)))
        {
            if (Guid.TryParse(id, out var filmId))
            {
                ids.Add(filmId);
            }
        }

        return ids;
    }

    public async Task Delete(Guid filmId)
    {
        try
        {
            await filmsContainer.DeleteItemAsync<Film>(
                filmId.ToString(),
                new PartitionKey(filmId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }

    public Task<int> Count()
    {
        var iterator = filmsContainer.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(ex, "{method}: error counting films.", nameof(Count)));
    }

    public IAsyncEnumerable<Film> GetAll()
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving films.", nameof(GetAll)));
    }

    public async Task<Film?> GetBy(Expression<Func<Film, bool>> selector)
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
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

    public IAsyncEnumerable<Film> GetAllBy(Expression<Func<Film, bool>> selector)
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving films with selector.", nameof(GetAllBy)));
    }

    public IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Film, bool>> selector,
        Expression<Func<Film, TProjection>> projection)
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving projected films.", nameof(GetAllBy)));
    }
}
