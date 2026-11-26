using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Posters;

namespace Indexer.Activities;

public class Tweet(
    ITweeter tweeter,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Tweet> logger)
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function(nameof(Tweet))]
    public async Task<IndexerContext> Run(
        [ActivityTrigger] IndexerContext indexerContext,
        FunctionContext functionContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Tweet));

        logger.LogInformation(
            "{TweetName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Tweet),
            functionContext.InvocationId);
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunTweet(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Tweet), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Tweet), reason);
        }

        try
        {
            await tweeter.Tweet(
                indexerContext is { SkipYouTubeUrlResolving: false, YouTubeError: false },
                indexerContext is { SkipSpotifyUrlResolving: false, SpotifyError: false },
                indexerContext.RecentEpisodeCandidates);
            logger.LogInformation("Tweet executed");
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        memoryProbe.End();

        logger.LogInformation($"{nameof(Run)} Completed");
        return indexerContext with { Success = true };
    }
}
