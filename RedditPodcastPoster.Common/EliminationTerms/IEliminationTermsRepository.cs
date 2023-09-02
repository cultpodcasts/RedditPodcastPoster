namespace RedditPodcastPoster.Common.EliminationTerms;

public interface IEliminationTermsRepository
{
    Task<EliminationTerms> Get();
    Task Save(EliminationTerms terms);
}