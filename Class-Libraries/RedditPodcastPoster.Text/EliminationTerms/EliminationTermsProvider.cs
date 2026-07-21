using EliminationTermsEntity = RedditPodcastPoster.Models.Subjects.EliminationTerms;

namespace RedditPodcastPoster.Text.EliminationTerms;

public class EliminationTermsProvider(EliminationTermsEntity eliminationTerms) : IEliminationTermsProvider
{
    public EliminationTermsEntity GetEliminationTerms()
    {
        return eliminationTerms;
    }
}
