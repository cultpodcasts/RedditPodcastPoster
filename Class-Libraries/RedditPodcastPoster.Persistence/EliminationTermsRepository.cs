using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EliminationTermsRepository(
    ILookupRepository lookupRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EliminationTermsRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IEliminationTermsRepository
{
    public async Task<EliminationTerms> Get()
    {
        return (await lookupRepository.GetEliminationTerms())!;
    }

    public async Task Save(EliminationTerms terms)
    {
        await lookupRepository.SaveEliminationTerms(terms);
    }
}