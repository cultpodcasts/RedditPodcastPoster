using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProviderFactory(
    ILookupRepositoryV2 lookupRepository,
    ILogger<EliminationTermsProviderFactory> logger)
    : IEliminationTermsProviderFactory
{
    public async Task<IEliminationTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(EliminationTermsProvider)}");
        var terms = await lookupRepository.GetEliminationTerms();
        return new EliminationTermsProvider(terms ?? new RedditPodcastPoster.Models.EliminationTerms());
    }
}