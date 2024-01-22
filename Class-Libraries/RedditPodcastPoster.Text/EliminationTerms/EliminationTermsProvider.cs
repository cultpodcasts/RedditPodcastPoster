namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProvider(Models.EliminationTerms eliminationTerms) : IEliminationTermsProvider
{
    public Models.EliminationTerms GetEliminationTerms()
    {
        return eliminationTerms;
    }
}