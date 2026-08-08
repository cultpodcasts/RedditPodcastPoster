using Microsoft.Extensions.Logging;
using EliminationTermsModel = RedditPodcastPoster.Models.Subjects.EliminationTerms;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.Persistence.Lookups;

public class EliminationTermsProviderFactory(
    IEliminationTermsRepository eliminationTermsRepository,
    ILogger<EliminationTermsProviderFactory> logger)
    : IEliminationTermsProviderFactory
{
    public async Task<IEliminationTermsProvider> Create(CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(EliminationTermsProvider)}");
        cancellationToken.ThrowIfCancellationRequested();
        var terms = await eliminationTermsRepository.Get();
        return new EliminationTermsProvider(terms ?? new EliminationTermsModel());
    }
}
