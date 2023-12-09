using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class OrchestrationTrigger
{
    private readonly ILogger<OrchestrationTrigger> _logger;

    public OrchestrationTrigger(ILogger<OrchestrationTrigger> logger)
    {
        _logger = logger;
    }

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
        _logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} initiated.");
        try
        {
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'. Status-Code: '{ex.StatusCode}', Status: '{ex.Status}'.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                $"Failure to execute '{nameof(client.ScheduleNewOrchestrationInstanceAsync)}' for '{nameof(Orchestration)}'.");
            throw;
        }

        _logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} complete.");
    }
}