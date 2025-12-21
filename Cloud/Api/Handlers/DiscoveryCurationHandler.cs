using System.Net;
using Api.Dtos;
using Api.Extensions;
using Api.Services;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Handlers;

public class DiscoveryCurationHandler(
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryUrlSubmitter discoveryUrlSubmitter,
    IEpisodeSearchIndexerService searchIndexerService,
    ILogger<DiscoveryCurationHandler> logger) : IDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Get(HttpRequestData r, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var result = await discoveryResultsService.Get(c);
            return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(result, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to obtain discovery-results.");
            return r.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    public async Task<HttpResponseData> Post(HttpRequestData r, DiscoverySubmitRequest m, ClientPrincipal? _,
        CancellationToken c)
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
            var submitOptions = new SubmitOptions(null, true);

            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(m);
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
                            EpisodeId = result.EpisodeId,
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
                        nameof(Post), discoveryResult.Id);
                }
            }

            await discoveryResultsService.MarkAsProcessed(m.DiscoveryResultsDocumentIds, m.ResultIds,
                erroredResults.ToArray());

            var episodeIds = submitResults
                .Where(x => x.EpisodeId != null)
                .Select(x => x.EpisodeId!.Value);
            var indexed = await searchIndexerService.IndexEpisodes(episodeIds, c);

            await discoveryResultsService.UpdateDiscoveryInfoContent();

            var response = new DiscoverySubmitResponse
            {
                Message = "Success",
                ErrorsOccurred = errorsOccured,
                Results = [.. submitResults],
                SearchIndexerState = indexed.ToDto()
            };

            return await r
                .CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(response, c);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure handling post of {nameofDiscoverySubmitRequest}",
                nameof(DiscoverySubmitRequest));
        }

        return await r.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(new { Message = "Failure" }, c);
    }
}