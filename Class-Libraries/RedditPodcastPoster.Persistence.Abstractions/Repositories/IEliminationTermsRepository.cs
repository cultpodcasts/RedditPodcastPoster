using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IEliminationTermsRepository
{
    Task<EliminationTerms> Get();
    Task Save(EliminationTerms terms);
}