using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public static class D1SearchServiceCollectionExtensions
{
    /// <summary>
    /// The D1 search writer (section <c>d1Search</c>). Safe to register everywhere: with no enabled, configured
    /// target (every console app, local runs) <see cref="ID1SearchIndexWriter.IsActive"/> is false and nothing is
    /// sent. The change feed and the reconcile need
    /// <c>AddRepositories()</c>.
    /// </summary>
    public static IServiceCollection AddD1SearchSync(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(D1SearchIndexWriter.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(D1SearchTokenSource.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
        services.TryAddSingleton<ID1SearchTokenSource, D1SearchTokenSource>();
        services.TryAddSingleton<D1SearchIndexWriter>();
        services.TryAddSingleton<ID1SearchIndexWriter>(s => s.GetRequiredService<D1SearchIndexWriter>());
        services.TryAddSingleton<ID1SearchInventoryReader>(s => s.GetRequiredService<D1SearchIndexWriter>());
        services.TryAddScoped<ID1SearchCatalogue, D1SearchCatalogue>();
        services.TryAddScoped<ID1SearchChangeFeed, D1SearchChangeFeed>();
        services.TryAddScoped<ID1SearchReconciler, D1SearchReconciler>();
        return services.BindConfiguration<D1SearchOptions>("d1Search");
    }
}
