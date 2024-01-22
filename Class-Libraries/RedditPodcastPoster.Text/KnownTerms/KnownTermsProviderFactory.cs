using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsProviderFactory(
    IKnownTermsRepository knownTermsRepository,
    ILogger<KnownTermsProviderFactory> logger)
    : IKnownTermsProviderFactory
{
    public async Task<IKnownTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(KnownTermsProvider)}");
        var knownTerms = await knownTermsRepository.Get();
        return new KnownTermsProvider(knownTerms);
    }
}