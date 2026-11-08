using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class ParentKindTransferJobStore(
    Container lookUpsContainer,
    ILogger<ParentKindTransferJobStore> logger) : IParentKindTransferJobStore
{
    public async Task<ParentKindTransferJob?> Get(Guid podcastId)
    {
        var id = ParentKindTransferJob.IdFor(podcastId);
        try
        {
            var response = await lookUpsContainer.ReadItemAsync<ParentKindTransferJob>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Save(ParentKindTransferJob job)
    {
        job.Id = ParentKindTransferJob.IdFor(job.PodcastId);
        try
        {
            await lookUpsContainer.UpsertItemAsync(job, new PartitionKey(job.Id.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method}: Failed to save kind-transfer job for podcast '{podcastId}' (phase {phase}).",
                nameof(Save),
                job.PodcastId,
                job.Phase);
            throw;
        }
    }
}
