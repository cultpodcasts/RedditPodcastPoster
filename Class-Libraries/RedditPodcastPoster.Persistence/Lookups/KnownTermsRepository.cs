using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.KnownTerms;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace RedditPodcastPoster.Persistence.Lookups;

public class KnownTermsRepository(
    ILookupRepository lookupRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<KnownTermsRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IKnownTermsRepository
{
    public async Task<KnownTermsModel> Get()
    {
        return (await lookupRepository.GetKnownTerms<KnownTermsModel>())!;
    }

    public Task Save(KnownTermsModel terms)
    {
        return lookupRepository.SaveKnownTerms(terms);
    }
}
