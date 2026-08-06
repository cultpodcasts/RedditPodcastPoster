using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Text.TitleCasing;

public interface ITitleCasingRulesProviderFactory : IAsyncFactory<ITitleCasingRulesProvider>;
