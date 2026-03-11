using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsProviderFactory(
    ILookupRepositoryV2 lookupRepository,
    ILogger<KnownTermsProviderFactory> logger)
    : IKnownTermsProviderFactory
{
    public async Task<IKnownTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(KnownTermsProvider)}");
        var knownTerms = await lookupRepository.GetKnownTerms<KnownTerms>();
        return new KnownTermsProvider(knownTerms ?? new KnownTerms());
    }
}