using Api.Dtos;
using RedditPodcastPoster.Models;

namespace Api.Services;

public interface IDiscoveryResultsService
{
    Task<DiscoveryResults> Get(CancellationToken c);
    Task MarkAsProcessed(Guid[] documentIds);
    Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoveryIngest discoveryIngest);
}