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
        return (await dataRepository.Read<KnownTerms>(KnownTerms._Id.ToString()))!;
    }

    public Task Save(KnownTerms terms)
    {
        return dataRepository.Write(terms);
    }
}