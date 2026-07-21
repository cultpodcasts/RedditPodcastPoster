using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.EliminationTerms;
using EliminationTermsModel = RedditPodcastPoster.Models.Subjects.EliminationTerms;

using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Persistence.Lookups;

public class EliminationTermsProviderFactory(
    IEliminationTermsRepository eliminationTermsRepository,
    ILogger<EliminationTermsProviderFactory> logger)
    : IEliminationTermsProviderFactory
{
    public async Task<IEliminationTermsProvider> Create()
    {
        logger.LogInformation($"{nameof(Create)} - Creating {nameof(EliminationTermsProvider)}");
        var terms = await eliminationTermsRepository.Get();
        return new EliminationTermsProvider(terms ?? new EliminationTermsModel());
    }
}
