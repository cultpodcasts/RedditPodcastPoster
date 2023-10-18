namespace RedditPodcastPoster.Common.EliminationTerms;

public interface IEliminationTermsProviderFactory
{
    Task<IEliminationTermsProvider> Create();
}