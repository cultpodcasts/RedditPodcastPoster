namespace RedditPodcastPoster.Common.EliminationTerms;

public class EliminationTermsProvider : IEliminationTermsProvider
{
    private readonly EliminationTerms _eliminationTerms;

    public EliminationTermsProvider(EliminationTerms eliminationTerms)
    {
        _eliminationTerms = eliminationTerms;
    }

    public EliminationTerms GetEliminationTerms()
    {
        return _eliminationTerms;
    }
}