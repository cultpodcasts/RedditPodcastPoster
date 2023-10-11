using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;

namespace RedditPodcastPoster.Common.EliminationTerms;

public class EliminationTermsRepository : IEliminationTermsRepository
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<PodcastRepository> _logger;

    public EliminationTermsRepository(
        IDataRepository dataRepository,
        ILogger<PodcastRepository> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<EliminationTerms> Get()
    {
        var partitionKey = EliminationTerms.PartitionKey;
        return (await _dataRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString(), partitionKey))!;
    }

    public async Task Save(EliminationTerms terms)
    {
        var key = terms.GetPartitionKey();
        await _dataRepository.Write(key, terms);
    }
}