using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class OrchestrationTrigger(ILogger<OrchestrationTrigger> logger)
{
    private static readonly TimeSpan OrchestrationDelay = TimeSpan.FromSeconds(10);

    [Function("OrchestrationTrigger")]
    public async Task Run(
        [TimerTrigger("0 */1 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} initiated.");
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'. Status-Code: '{ex.StatusCode}', Status: '{ex.Status}'.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'.");
            throw;
        }

        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} complete. Instance-id= '{instanceId}'.");
    }
}