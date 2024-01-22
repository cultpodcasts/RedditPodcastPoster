using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsRepository(
    IDataRepository dataRepository,
    ILogger<KnownTermsRepository> logger)
    : IKnownTermsRepository
{
    public async Task<KnownTerms> Get()
    {
        var partitionKey = new KnownTerms().GetPartitionKey();
        return (await dataRepository.Read<KnownTerms>(KnownTerms._Id.ToString(), partitionKey))!;
    }

    public async Task Save(KnownTerms terms)
    {
        var key = terms.GetPartitionKey();
        await dataRepository.Write(terms);
    }
}