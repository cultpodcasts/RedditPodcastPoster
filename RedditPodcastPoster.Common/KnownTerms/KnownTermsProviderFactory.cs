using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;

namespace RedditPodcastPoster.Common.KnownTerms;

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