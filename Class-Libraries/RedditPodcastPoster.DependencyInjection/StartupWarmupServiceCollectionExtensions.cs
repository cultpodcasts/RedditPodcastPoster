using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace RedditPodcastPoster.DependencyInjection;

public static class StartupWarmupServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IStartupWarmer"/> and ensures a single
    /// <see cref="ParallelStartupWarmupHostedService"/> runs all warmers in parallel.
    /// </summary>
    public static IServiceCollection AddStartupWarmer<TWarmer>(this IServiceCollection services)
        where TWarmer : class, IStartupWarmer
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupWarmer, TWarmer>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ParallelStartupWarmupHostedService>());
        return services;
    }
}
