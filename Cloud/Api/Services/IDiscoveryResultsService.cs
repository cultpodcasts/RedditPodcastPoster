using Api.Dtos;
using RedditPodcastPoster.Models;

namespace Api.Services;

public interface IDiscoveryResultsService
{
    Task<DiscoveryResponse> Get(CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds);
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest);
}