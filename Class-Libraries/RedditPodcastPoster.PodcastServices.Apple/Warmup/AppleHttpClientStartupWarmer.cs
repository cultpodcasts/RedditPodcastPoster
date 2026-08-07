using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.Apple.Warmup;

/// <summary>
/// Warms Apple podcast HTTP client (bearer scrape + <see cref="HttpClient"/> cache).
/// </summary>
public sealed class AppleHttpClientStartupWarmer(IAsyncInstance<HttpClient> appleHttpClient) : IStartupWarmer
{
    public string Name => "ApplePodcastHttpClient";

    public Task WarmAsync(CancellationToken cancellationToken) => appleHttpClient.GetAsync(cancellationToken);
}
