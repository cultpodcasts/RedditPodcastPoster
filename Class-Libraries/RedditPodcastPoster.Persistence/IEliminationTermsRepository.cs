namespace RedditPodcastPoster.Persistence;

public interface IEliminationTermsRepository
{
    Task<Models.EliminationTerms> Get();
    Task Save(Models.EliminationTerms terms);
}