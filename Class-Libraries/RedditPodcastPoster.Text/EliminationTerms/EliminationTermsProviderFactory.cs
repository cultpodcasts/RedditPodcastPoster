using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProviderFactory(
    IEliminationTermsRepository eliminationTermsRepository,
    ILogger<EliminationTermsProviderFactory> logger)
    : IEliminationTermsProviderFactory
{
    public async Task<IEliminationTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(EliminationTermsProvider)}");
        var knownTerms = await eliminationTermsRepository.Get();
        return new EliminationTermsProvider(knownTerms);
    }
}