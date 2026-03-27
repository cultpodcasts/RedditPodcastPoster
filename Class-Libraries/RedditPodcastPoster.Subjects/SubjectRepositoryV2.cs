using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectRepositoryV2(
    Container subjectsContainer,
    ILogger<SubjectRepositoryV2> logger)
    : ISubjectRepositoryV2, ISubjectsProvider
{
    public async Task Save(Subject subject)
    {
        await subjectsContainer.UpsertItemAsync(subject, new PartitionKey(subject.Id.ToString()));
    }

    public async Task<int> Count()
    {
        var iterator = subjectsContainer.GetItemQueryIterator<int>(
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
                logger.LogError(ex, "{method}: error counting subjects.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<Subject> GetAll()
    {
        var query = subjectsContainer.GetItemLinqQueryable<Subject>(requestOptions: new QueryRequestOptions());
        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Subject> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving subjects.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public Task<Subject?> GetByName(string name)
    {
        return GetBy(x => x.Name == name);
    }

    public async Task<Subject?> GetBy(Expression<Func<Subject, bool>> selector)
    {
        var query = subjectsContainer
            .GetItemLinqQueryable<Subject>(requestOptions: new QueryRequestOptions())
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

    public async IAsyncEnumerable<Subject> GetAllBy(Expression<Func<Subject, bool>> selector)
    {
        var query = subjectsContainer
            .GetItemLinqQueryable<Subject>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Subject> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving filtered subjects.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Subject, bool>> selector,
        Expression<Func<Subject, TProjection>> projection)
    {
        var query = subjectsContainer
            .GetItemLinqQueryable<Subject>(requestOptions: new QueryRequestOptions())
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
                logger.LogError(ex, "{method}: error retrieving projected subjects.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
