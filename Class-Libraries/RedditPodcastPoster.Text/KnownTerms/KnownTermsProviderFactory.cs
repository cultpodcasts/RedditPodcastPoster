using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsProviderFactory : IKnownTermsProviderFactory
{
    private readonly IKnownTermsRepository _knownTermsRepository;
    private readonly ILogger<KnownTermsProviderFactory> _logger;

    public KnownTermsProviderFactory(
        IKnownTermsRepository knownTermsRepository,
        ILogger<KnownTermsProviderFactory> logger)
    {
        _knownTermsRepository = knownTermsRepository;
        _logger = logger;
    }
    public async Task<IKnownTermsProvider> Create()
    {
        _logger.LogInformation($"{nameof(Create)} - Creating {nameof(KnownTermsProvider)}");
        var knownTerms = await _knownTermsRepository.Get();
        return new KnownTermsProvider(knownTerms);
    }
}