using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.People.Repositories;

public class PersonRepository(
    Container peopleContainer,
    ILogger<PersonRepository> logger)
    : IPersonRepository
{
    public async Task Save(Person person)
    {
        person.EnsureNameKey();
        // People container partitions on /type so UniqueKeyPolicy(/nameKey) is global for all Person docs.
        await peopleContainer.UpsertItemAsync(person, new PartitionKey(person.ModelType.ToString()));
    }

    public async Task<int> Count()
    {
        var iterator = peopleContainer.GetItemQueryIterator<int>(
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
                logger.LogError(ex, "{method}: error counting people.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<Person> GetAll()
    {
        var query = peopleContainer.GetItemLinqQueryable<Person>(requestOptions: new QueryRequestOptions());
        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Person> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving people.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public Task<Person?> GetByName(string name)
    {
        var nameKey = Person.NormalizeNameKey(name);
        if (string.IsNullOrEmpty(nameKey))
        {
            return Task.FromResult<Person?>(null);
        }

        // Prefer nameKey; fall back to LOWER(name) for documents not yet backfilled.
        return GetBy(x => x.NameKey == nameKey || x.Name.ToLower() == nameKey);
    }

    public async Task<Person?> GetBy(Expression<Func<Person, bool>> selector)
    {
        var query = peopleContainer
            .GetItemLinqQueryable<Person>(requestOptions: new QueryRequestOptions())
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

    public async IAsyncEnumerable<Person> GetAllBy(Expression<Func<Person, bool>> selector)
    {
        var query = peopleContainer
            .GetItemLinqQueryable<Person>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Person> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving filtered people.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Person, bool>> selector,
        Expression<Func<Person, TProjection>> projection)
    {
        var query = peopleContainer
            .GetItemLinqQueryable<Person>(requestOptions: new QueryRequestOptions())
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
                logger.LogError(ex, "{method}: error retrieving projected people.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
