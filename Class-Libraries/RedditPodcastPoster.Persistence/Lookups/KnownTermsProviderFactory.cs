using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Text.KnownTerms;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace RedditPodcastPoster.Persistence.Lookups;

public class KnownTermsProviderFactory(
    IKnownTermsRepository knownTermsRepository,
    ILogger<KnownTermsProviderFactory> logger)
    : IKnownTermsProviderFactory
{
    public async Task<IKnownTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(KnownTermsProvider)}");
        var knownTerms = await knownTermsRepository.Get();
        return new KnownTermsProvider(knownTerms ?? new KnownTermsModel());
    }
}
