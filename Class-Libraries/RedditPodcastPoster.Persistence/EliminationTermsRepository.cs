using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EliminationTermsRepository : IEliminationTermsRepository
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<EliminationTermsRepository> _logger;

    public EliminationTermsRepository(
        IDataRepository dataRepository,
        ILogger<EliminationTermsRepository> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<Models.EliminationTerms> Get()
    {
        var partitionKey = Models.EliminationTerms.PartitionKey;
        return (await _dataRepository.Read<Models.EliminationTerms>(Models.EliminationTerms._Id.ToString(), partitionKey))!;
    }

    public async Task Save(Models.EliminationTerms terms)
    {
        var key = terms.GetPartitionKey();
        await _dataRepository.Write(terms);
    }
}