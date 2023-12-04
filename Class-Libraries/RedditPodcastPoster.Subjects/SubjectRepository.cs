using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectRepository : ISubjectRepository
{
    private readonly Container _container;
    private readonly ILogger<SubjectRepository> _logger;
    private readonly IRepository<Subject> _repository;

    public SubjectRepository(
        IRepository<Subject> repository,
        Container container,
        ILogger<SubjectRepository> logger)
    {
        _repository = repository;
        _container = container;
        _logger = logger;
    }

    public Task<IEnumerable<Subject>> GetAll()
    {
        return _repository.GetAll(Subject.PartitionKey);
    }

    public async Task<Subject?> GetByName(string name)
    {
        using var query = _container
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