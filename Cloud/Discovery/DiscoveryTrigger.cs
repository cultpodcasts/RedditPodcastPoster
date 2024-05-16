using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Discovery;

public class DiscoveryTrigger
{
    private readonly ILogger _logger;

    public DiscoveryTrigger(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DiscoveryTrigger>();
    }

    [Function("DiscoveryTrigger")]
    public void Run([TimerTrigger("0 */6 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }
    }
}