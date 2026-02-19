using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Text.EliminationTerms;

public interface IEliminationTermsProviderFactory : IAsyncFactory<IEliminationTermsProvider>
{
}