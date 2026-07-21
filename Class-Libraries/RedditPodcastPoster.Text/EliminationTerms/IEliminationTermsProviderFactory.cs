using RedditPodcastPoster.DependencyInjection;

using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Text.EliminationTerms;

public interface IEliminationTermsProviderFactory : IAsyncFactory<IEliminationTermsProvider>
{
}