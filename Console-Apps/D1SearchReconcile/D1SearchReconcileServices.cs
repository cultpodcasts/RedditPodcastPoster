using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Persistence.Extensions;

namespace D1SearchReconcile;

public static class D1SearchReconcileServices
{
    /// <summary>
    /// The console's registrations: Cosmos repositories and the D1 search sync (writer, catalogue, reconcile).
    /// <c>requireEnabled</c> is false: the console does not use <c>d1Search:Enabled</c> and stays dry unless
    /// <c>--write</c>.
    /// </summary>
    public static IServiceCollection AddD1SearchReconcileConsoleServices(this IServiceCollection services) =>
        services
            .AddLogging()
            .AddRepositories()
            .AddD1SearchSync(requireEnabled: false);
}
