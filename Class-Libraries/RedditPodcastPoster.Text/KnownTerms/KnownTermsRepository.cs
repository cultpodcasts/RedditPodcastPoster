using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsRepository(
    ILookupRepository lookupRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<KnownTermsRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IKnownTermsRepository
{
    public async Task<KnownTerms> Get()
    {
        return (await lookupRepository.GetKnownTerms<KnownTerms>())!;
    }

    public Task Save(KnownTerms terms)
    {
        return lookupRepository.SaveKnownTerms(terms);
    }
}