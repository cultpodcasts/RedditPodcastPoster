using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Persistence.Extensions;

namespace D1SearchReconcile;

public static class D1SearchReconcileServices
{
    /// <summary>The console's registrations: Cosmos repositories and the D1 search sync (writer, catalogue, reconcile).</summary>
    public static IServiceCollection AddD1SearchReconcileConsoleServices(this IServiceCollection services) =>
        services
            .AddLogging()
            .AddRepositories()
            .AddD1SearchSync();
}
