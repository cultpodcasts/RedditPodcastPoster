using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class HostCompositionValidator
{
    public const string LoggerCategory = "HostComposition";

    public static async Task ValidateAsync(
        IHost host,
        string hostName,
        IEnumerable<Type> canaryServiceTypes,
        CancellationToken cancellationToken = default)
    {
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        var canaryTypes = canaryServiceTypes.ToArray();

        logger.LogWarning(
            "Host composition validation starting for {HostName}. Canary services: {CanaryCount}.",
            hostName,
            canaryTypes.Length);

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var scopedProvider = scope.ServiceProvider;

            foreach (var serviceType in canaryTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ResolveFromScope(scopedProvider, serviceType);
                logger.LogWarning(
                    "Host composition validated {ServiceType} for {HostName}.",
                    serviceType.Name,
                    hostName);
            }

            logger.LogWarning(
                "Host composition validation succeeded for {HostName}.",
                hostName);
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Host composition validation failed for {HostName}. The function host will not start.",
                hostName);
            throw;
        }
    }

    public static void ResolveFromScope(IServiceProvider scopedProvider, Type serviceType)
    {
        if (serviceType.IsInterface || serviceType.IsAbstract)
        {
            scopedProvider.GetRequiredService(serviceType);
            return;
        }

        ActivatorUtilities.CreateInstance(scopedProvider, serviceType);
    }
}
