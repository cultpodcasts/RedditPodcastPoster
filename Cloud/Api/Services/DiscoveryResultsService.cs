using Api.Dtos;
using RedditPodcastPoster.Discovery;

namespace Api.Services;

public class DiscoveryResultsService(IDiscoveryResultsRepository discoveryResultsRepository) : IDiscoveryResultsService
{
    public async Task<DiscoveryResults> Get(CancellationToken c)
    {
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(c);
        var results = documents.SelectMany(x => x.DiscoveryResults);
        var result = new DiscoveryResults
        {
            Ids = documents.Select(x => x.Id),
            Results = results.OrderBy(x => x.Released)
        };
        return result;
    }
}