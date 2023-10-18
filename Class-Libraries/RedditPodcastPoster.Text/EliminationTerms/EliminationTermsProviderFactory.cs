using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProviderFactory : IEliminationTermsProviderFactory
{
    private readonly IEliminationTermsRepository _eliminationTermsRepository;
    private readonly ILogger<EliminationTermsProviderFactory> _logger;

    public EliminationTermsProviderFactory(
        IEliminationTermsRepository eliminationTermsRepository,
        ILogger<EliminationTermsProviderFactory> logger)
    {
        _eliminationTermsRepository = eliminationTermsRepository;
        _logger = logger;
    }
    public async Task<IEliminationTermsProvider> Create()
    {
        _logger.LogInformation($"{nameof(Create)} - Creating {nameof(EliminationTermsProvider)}");
        var knownTerms = await _eliminationTermsRepository.Get();
        return new EliminationTermsProvider(knownTerms);
    }
}