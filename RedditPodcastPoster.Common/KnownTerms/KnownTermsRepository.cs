using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;

namespace RedditPodcastPoster.Common.KnownTerms;

public class KnownTermsRepository : IKnownTermsRepository
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<PodcastRepository> _logger;

    public KnownTermsRepository(
        IDataRepository dataRepository,
        ILogger<PodcastRepository> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<KnownTerms> Get()
    {
        var partitionKey = new KnownTerms().GetPartitionKey();
        return (await _dataRepository.Read<KnownTerms>(KnownTerms._Id.ToString(), partitionKey))!;
    }

    public async Task Save(KnownTerms terms)
    {
        var key = terms.GetPartitionKey();
        await _dataRepository.Write(key, terms);
    }
}