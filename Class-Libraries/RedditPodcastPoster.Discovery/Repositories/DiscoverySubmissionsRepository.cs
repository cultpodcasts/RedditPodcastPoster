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

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await submissionsContainer.DeleteItemAsync<DiscoverySubmission>(id.ToString(),
                new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }
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

    public async IAsyncEnumerable<DiscoverySubmission> GetBySubmitter(
        string submitterSubject,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var iterator = submissionsContainer
            .GetItemLinqQueryable<DiscoverySubmission>()
            .Where(x => x.SubmitterSubject == submitterSubject)
            .ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public async Task<string?> TryReplace(DiscoverySubmission submission, string etag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await submissionsContainer.ReplaceItemAsync(submission, submission.Id.ToString(),
                new PartitionKey(submission.Id.ToString()), new ItemRequestOptions { IfMatchEtag = etag },
                cancellationToken);
            return response.ETag;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
