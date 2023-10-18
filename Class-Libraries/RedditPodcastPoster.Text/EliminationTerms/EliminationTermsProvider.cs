namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProvider : IEliminationTermsProvider
{
    private readonly Models.EliminationTerms _eliminationTerms;

    public EliminationTermsProvider(Models.EliminationTerms eliminationTerms)
    {
        _eliminationTerms = eliminationTerms;
    }

    public Models.EliminationTerms GetEliminationTerms()
    {
        return _eliminationTerms;
    }
}