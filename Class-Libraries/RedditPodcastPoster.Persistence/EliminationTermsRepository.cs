using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EliminationTermsRepository(
    IDataRepository dataRepository,
    ILogger<EliminationTermsRepository> logger)
    : IEliminationTermsRepository
{
    private readonly ILogger<EliminationTermsRepository> _logger = logger;

    public async Task<EliminationTerms> Get()
    {
        return (await dataRepository.Read<EliminationTerms>(EliminationTerms._Id.ToString()))!;
    }

    public async Task Save(EliminationTerms terms)
    {
        await dataRepository.Write(terms);
    }
}