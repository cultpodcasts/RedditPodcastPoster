using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IEliminationTermsRepository
{
    Task<EliminationTerms> Get();
    Task Save(EliminationTerms terms);
}