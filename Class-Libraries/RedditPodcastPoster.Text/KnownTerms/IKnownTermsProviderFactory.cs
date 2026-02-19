using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Text.KnownTerms;

public interface IKnownTermsProviderFactory : IAsyncFactory<IKnownTermsProvider>
{
}