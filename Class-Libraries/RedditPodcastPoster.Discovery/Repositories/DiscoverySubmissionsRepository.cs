using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Discovery.Repositories;

public class DiscoverySubmissionsRepository(Container submissionsContainer) : IDiscoverySubmissionsRepository
{
    public async Task Save(DiscoverySubmission submission, CancellationToken cancellationToken = default)
    {
        await submissionsContainer.UpsertItemAsync(submission, new PartitionKey(submission.Id.ToString()),
            cancellationToken: cancellationToken);
    }

    public async Task<DiscoverySubmission?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await submissionsContainer.ReadItemAsync<DiscoverySubmission>(id.ToString(),
                new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<DiscoverySubmission> GetByStatus(
        DiscoverySubmissionStatus status,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var iterator = submissionsContainer
            .GetItemLinqQueryable<DiscoverySubmission>()
            .Where(x => x.Status == status)
            .ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }
}
