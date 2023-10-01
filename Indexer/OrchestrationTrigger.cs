using Amazon.Runtime.Internal.Util;
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
        )] TimerInfo info,
        [DurableClient] DurableTaskClient client)
    {
        _logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} initiated.");
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestration));
        _logger.LogInformation($"{nameof(OrchestrationTrigger)} {nameof(Run)} complete.");

    }
}