using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class OrchestrationTrigger(ILogger<OrchestrationTrigger> logger)
{
    private static readonly TimeSpan OrchestrationDelay = TimeSpan.FromSeconds(10);

    [Function("Hourly")]
    public async Task RunHourly(
        [TimerTrigger("0 */1 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(RunHourly)} initiated.");
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HourlyOrchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HourlyOrchestration)}'. Status-Code: '{ex.StatusCode}', Status: '{ex.Status}'.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HourlyOrchestration)}'.");
            throw;
        }

        logger.LogInformation(
            $"{nameof(OrchestrationTrigger)} {nameof(RunHourly)} complete. Instance-id= '{instanceId}'.");
    }


    [Function("HalfHourly")]
    public async Task RunHalfHourly(
        [TimerTrigger("30 */1 * * *"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo info,
        [DurableClient] DurableTaskClient client)
    {
        logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(RunHalfHourly)} initiated.");
        string instanceId;
        try
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HalfHourlyOrchestration));
        }
        catch (RpcException ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HalfHourlyOrchestration)}'. Status-Code: '{ex.StatusCode}', Status: '{ex.Status}'.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(HalfHourlyOrchestration)}'.");
            throw;
        }

        logger.LogInformation(
            $"{nameof(OrchestrationTrigger)} {nameof(RunHalfHourly)} complete. Instance-id= '{instanceId}'.");
    }
}