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
        await filmsContainer.UpsertItemAsync(film, new PartitionKey(film.Id.ToString()));
    }

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
        while (iterator.HasMoreResults)
        {
            FeedResponse<string> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "{method}: error finding films holding trailer '{trailerId}'.",
                    nameof(GetFilmIdsByTrailerId), trailerId);
                throw;
            }

            foreach (var id in response)
            {
                if (Guid.TryParse(id, out var filmId))
                {
                    ids.Add(filmId);
                }
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

    public async Task<int> Count()
    {
        var iterator = filmsContainer.GetItemQueryIterator<int>(
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
                logger.LogError(ex, "{method}: error counting films.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<Film> GetAll()
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
            .AsQueryable();

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Film> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving films.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
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

    public async IAsyncEnumerable<Film> GetAllBy(Expression<Func<Film, bool>> selector)
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Film> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving films with selector.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Film, bool>> selector,
        Expression<Func<Film, TProjection>> projection)
    {
        var query = filmsContainer
            .GetItemLinqQueryable<Film>(requestOptions: new QueryRequestOptions())
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
                logger.LogError(ex, "{method}: error retrieving projected films.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
