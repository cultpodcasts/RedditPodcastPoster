using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Persistence.Lookups;

public sealed class TitleCasingRulesStartupWarmer(
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider) : IStartupWarmer
{
    public string Name => nameof(ITitleCasingRulesProvider);

    public Task WarmAsync(CancellationToken cancellationToken) =>
        titleCasingRulesProvider.GetAsync(cancellationToken);
}
