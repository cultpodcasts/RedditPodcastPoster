using Api.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Services.Discovery;

public interface IDiscoveryResultsService
{
    Task<DiscoveryCurationData> Get(bool includeHidden, CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds);
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest);
    Task UpdateDiscoveryInfoContent();
}
