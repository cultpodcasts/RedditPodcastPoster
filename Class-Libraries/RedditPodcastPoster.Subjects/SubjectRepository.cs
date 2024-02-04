using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
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
        return repository.GetAll();
    }

    public async Task<Subject?> GetByName(string name)
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<Subject>().ToString();
        using var query = container
            .GetItemLinqQueryable<Subject>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
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

    public async Task<List<Subject>> GetByNames(string[] names)
    {
        var partitionKey = CosmosSelectorExtensions.GetModelType<Subject>().ToString();
        var subjects = new List<Subject>();
        using var query = container
            .GetItemLinqQueryable<Subject>(
                linqSerializerOptions: new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(partitionKey)
                })
            .Where(x => names.Contains(x.Name))
            .ToFeedIterator();
        if (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                {
                    subjects.Add(item);
                }
            }
        }

        return subjects;
    }
}