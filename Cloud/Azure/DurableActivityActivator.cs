using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class DurableActivityActivator
{
    public static TActivity Create<TActivity>(FunctionContext executionContext, string activityName)
        where TActivity : class
    {
        var logger = executionContext.GetLogger(nameof(DurableActivityActivator));
        try
        {
            return ActivatorUtilities.CreateInstance<TActivity>(executionContext.InstanceServices);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to activate durable activity {ActivityName}. Check dependency injection registration.",
                activityName);
            throw;
        }
    }
}
