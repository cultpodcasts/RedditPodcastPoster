using Indexer.Publishing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class Publisher
{
    private readonly IContentPublisher _contentPublisher;
    private readonly ILogger _logger;

    public Publisher(
        IContentPublisher contentPublisher,
        ILoggerFactory loggerFactory)
    {
        _contentPublisher = contentPublisher;
        _logger = loggerFactory.CreateLogger<Publisher>();
    }

    [Function("ContentPublisher")]
    public async Task Run([TimerTrigger("10 */1 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo timerTimer
    )
    {
        _logger.LogInformation($"{nameof(Indexer)}.{nameof(Run)} Initiated.");

        await _contentPublisher.Publish();

        _logger.LogInformation($"{nameof(Run)} Completed");
    }
}