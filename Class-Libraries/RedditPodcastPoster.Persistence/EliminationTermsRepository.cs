using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EliminationTermsRepository(
    IDataRepository dataRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EliminationTermsRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IEliminationTermsRepository
{
    public async Task<EliminationTerms> Get()
    {
        return (await dataRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString()))!;
    }

    public async Task Save(EliminationTerms terms)
    {
        await dataRepository.Write(terms);
    }
}