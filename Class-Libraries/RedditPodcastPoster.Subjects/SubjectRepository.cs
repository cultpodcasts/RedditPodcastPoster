using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectRepository(
    IRepository<Subject> repository,
    Container container,
    ILogger<SubjectRepository> logger)
    : ISubjectRepository
{
    public Task<IEnumerable<Subject>> GetAll()
    {
        return repository.GetAll(Subject.PartitionKey);
    }

    public async Task<Subject?> GetByName(string name)
    {
        using var query = container
            .GetItemLinqQueryable<Subject>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(Subject.PartitionKey)
                })
            .Where(x => x.Name == name)
            .ToFeedIterator();
        if (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                {
                    return item;
                }
            }
        }

        return null;
    }
}