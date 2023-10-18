namespace RedditPodcastPoster.Text.EliminationTerms;

public interface IEliminationTermsProviderFactory
{
    Task<IEliminationTermsProvider> Create();
}