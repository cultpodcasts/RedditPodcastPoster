using EliminationTermsEntity = RedditPodcastPoster.Models.Subjects.EliminationTerms;

namespace RedditPodcastPoster.Text.EliminationTerms;

public interface IEliminationTermsProvider
{
    EliminationTermsEntity GetEliminationTerms();
}
