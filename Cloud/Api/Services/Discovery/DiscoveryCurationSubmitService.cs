using Api.Dtos;
using Api.Extensions;
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
            var submitResults = new List<DiscoverySubmitResponseItem>();
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
                        new DiscoverySubmitResponseItem
                        {
                            DiscoveryItemId = discoveryResult.Id,
                            EpisodeId = result.Episode?.Id,
                            PodcastId = result.Episode?.PodcastId,
                            Message = result.State.ToString()
                        });
                }
                catch (Exception ex)
                {
                    erroredResults.Add(discoveryResult.Id);
                    errorsOccured = true;
                    submitResults.Add(
                        new DiscoverySubmitResponseItem
                        {
                            DiscoveryItemId = discoveryResult.Id,
                            Message = "Error"
                        });
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

            var response = new DiscoverySubmitResponse
            {
                Message = "Success",
                ErrorsOccurred = errorsOccured,
                Results = [.. submitResults],
                SearchIndexerState = indexed.ToDto()
            };

            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok, response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure handling post of {nameofDiscoverySubmitRequest}",
                nameof(DiscoverySubmitRequest));
            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Failed);
        }
    }
}
