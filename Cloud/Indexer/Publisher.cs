using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;

namespace Indexer;

[DurableTask(nameof(Publisher))]
public class Publisher : TaskActivity<IndexerResponse, IndexerResponse>
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

    public override async Task<IndexerResponse> RunAsync(TaskActivityContext context, IndexerResponse indexerResponse)
    {
        _logger.LogInformation($"{nameof(Publisher)} initiated.");

        if (DryRun.IsDryRun)
        {
            return indexerResponse with {Success = true};
        }

        try
        {
            await _contentPublisher.Publish();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IContentPublisher)}.{nameof(IContentPublisher.Publish)}.");
            return indexerResponse with {Success = false};
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerResponse with {Success = true};
    }
}