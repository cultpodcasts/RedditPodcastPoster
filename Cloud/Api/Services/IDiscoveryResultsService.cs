using Api.Dtos;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Services;

public interface IDiscoveryResultsService
{
    Task<DiscoveryResponse> Get(bool includeHidden, CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds);
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest);
    Task UpdateDiscoveryInfoContent();
}