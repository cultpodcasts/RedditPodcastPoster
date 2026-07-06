using Microsoft.Extensions.DependencyInjection;

namespace Azure;

public static class HostCompositionValidator
{
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
