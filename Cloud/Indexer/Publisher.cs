using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;

namespace Indexer;

[DurableTask(nameof(Publisher))]
public class Publisher : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IActivityMarshaller _activityMarshaller;
    private readonly IContentPublisher _contentPublisher;
    private readonly ILogger _logger;

    public Publisher(
        IContentPublisher contentPublisher,
        IActivityMarshaller activityMarshaller,
        ILoggerFactory loggerFactory)
    {
        _contentPublisher = contentPublisher;
        _activityMarshaller = activityMarshaller;
        _logger = loggerFactory.CreateLogger<Publisher>();
    }

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation(
            $"{nameof(Publisher)} initiated. Instance-id: '{context.InstanceId}', Publisher-Operation-Id: '{indexerContext.PublisherOperationId}'.");

        if (DryRun.IsPublisherDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.PublisherOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.PublisherOperationId));
        }


        try
        {
            Task[] publishingTasks;
            if (DateTime.UtcNow.Hour == 12)
            {
                publishingTasks = new[]
                {
                    _contentPublisher.PublishHomepage()
                };
            }
            else
            {
                publishingTasks = new[]
                {
                    _contentPublisher.PublishHomepage()
                };
            }

            await Task.WhenAll(publishingTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IContentPublisher)}.{nameof(IContentPublisher.PublishHomepage)}.");
            return indexerContext with {Success = false};
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}