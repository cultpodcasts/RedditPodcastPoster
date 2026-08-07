using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class EliminationTermsStartupWarmer(
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProvider) : IStartupWarmer
{
    public string Name => nameof(IEliminationTermsProvider);

    public Task WarmAsync(CancellationToken cancellationToken) =>
        eliminationTermsProvider.GetAsync();
}
