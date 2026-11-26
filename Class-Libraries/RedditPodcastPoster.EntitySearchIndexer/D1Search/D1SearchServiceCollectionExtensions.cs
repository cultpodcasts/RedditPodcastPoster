using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Configuration;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public static class D1SearchServiceCollectionExtensions
{
    /// <summary>
    /// The D1 search writer (section <c>d1Search</c>, plus the existing <c>api</c> and <c>auth0client</c> sections).
    /// Safe to register everywhere: unless it is configured (and, in the Functions, enabled)
    /// <see cref="ID1SearchIndexWriter.IsActive"/> is false and nothing is sent. The
    /// <c>D1SearchReconcile</c> console passes <paramref name="requireEnabled"/> false: it stays dry unless
    /// <c>--write</c> and does not use <c>d1Search:Enabled</c>. The change feed and the reconcile need
    /// <c>AddRepositories()</c>.
    /// </summary>
    public static IServiceCollection AddD1SearchSync(this IServiceCollection services, bool requireEnabled = true)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(D1SearchIndexWriter.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(D1SearchTokenSource.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
        services.TryAddSingleton<ID1SearchTokenSource, D1SearchTokenSource>();
        services.TryAddSingleton<D1SearchIndexWriter>();
        services.TryAddSingleton<ID1SearchIndexWriter>(s => s.GetRequiredService<D1SearchIndexWriter>());
        services.TryAddSingleton<ID1SearchInventoryReader>(s => s.GetRequiredService<D1SearchIndexWriter>());
        services.TryAddSingleton<ID1SearchRebuildClient>(s => s.GetRequiredService<D1SearchIndexWriter>());
        services.TryAddScoped<ID1SearchCatalogue, D1SearchCatalogue>();
        services.TryAddScoped<ID1SearchChangeFeed, D1SearchChangeFeed>();
        services.TryAddScoped<ID1SearchReconciler, D1SearchReconciler>();
        services.TryAddScoped<ID1SearchRebuilder, D1SearchRebuilder>();
        services.BindConfiguration<ApiOptions>("api");
        services.BindConfiguration<Auth0Options>("auth0client");
        services.BindConfiguration<D1SearchOptions>("d1Search");
        services.TryAddSingleton(sp => new D1SearchConnection(
            sp.GetRequiredService<IOptions<D1SearchOptions>>().Value,
            sp.GetRequiredService<IOptions<ApiOptions>>().Value,
            sp.GetRequiredService<IOptions<Auth0Options>>().Value,
            requireEnabled));
        return services;
    }
}
