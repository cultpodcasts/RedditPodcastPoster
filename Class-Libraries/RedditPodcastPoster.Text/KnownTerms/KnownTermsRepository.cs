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
        var knownTerms = await dataRepository.Read<KnownTerms>(KnownTerms._Id.ToString());
        return knownTerms!;
    }

    public Task Save(KnownTerms terms)
    {
        return dataRepository.Write(terms);
    }
}