using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace Api.Services.Discovery;

public class DiscoveryCurationSubmitService(
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryUrlSubmitter discoveryUrlSubmitter,
    IEpisodeSearchIndexerService searchIndexerService,
    ILogger<DiscoveryCurationSubmitService> logger) : IDiscoveryCurationSubmitService
{
    public async Task<DiscoveryCurationSubmitResult> SubmitAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var errorsOccured = false;
            var indexingContext = new IndexingContext
            {
                SkipPodcastDiscovery = false,
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false
            };
            var submitOptions = new SubmitOptions(null, true, CreationSource: EpisodeCreationSource.Discovery);

            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(request);
            var submitResults = new List<DiscoverySubmitItemOutcome>();
            var erroredResults = new List<Guid>();

            foreach (var discoveryResult in discoveryResults)
            {
                logger.LogInformation(
                    "Submitting discovery-result '{discoveryResultId}' with indexing-context: {indexingContext}",
                    discoveryResult.Id, indexingContext);
                try
                {
                    var result = await discoveryUrlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                    submitResults.Add(
                        new DiscoverySubmitItemOutcome(
                            discoveryResult.Id,
                            result.Episode?.Id,
                            result.Episode?.PodcastId,
                            result.State.ToString()));
                }
                catch (Exception ex)
                {
                    erroredResults.Add(discoveryResult.Id);
                    errorsOccured = true;
                    submitResults.Add(
                        new DiscoverySubmitItemOutcome(discoveryResult.Id, null, null, "Error"));
                    logger.LogError(ex, "{method} Failure submitting discovery-result '{discoveryResultId}'.",
                        nameof(SubmitAsync), discoveryResult.Id);
                }
            }

            await discoveryResultsService.MarkAsProcessed(request.DiscoveryResultsDocumentIds, request.ResultIds,
                erroredResults.ToArray());

            var episodeIds = submitResults
                .Where(x => x.EpisodeId != null)
                .Select(x => x.EpisodeId!.Value);
            var indexed = await searchIndexerService.IndexEpisodes(episodeIds, cancellationToken);

            await discoveryResultsService.UpdateDiscoveryInfoContent();

            var outcome = new DiscoverySubmitOutcome(
                "Success",
                errorsOccured,
                submitResults,
                indexed);

            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok, outcome);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure handling post of {nameofDiscoverySubmitRequest}",
                nameof(DiscoverySubmitRequest));
            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed);
        }
    }
}
