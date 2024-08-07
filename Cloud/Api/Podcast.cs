﻿using System.Net;
using System.Text.Json;
using Api.Dtos;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;

namespace Api;

public class Podcast(
    IIndexer indexer,
    ISearchIndexerService searchIndexerService,
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    ILogger<Podcast> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<IndexerOptions> indexerOptions,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    [Function("PodcastIndex")]
    public Task<HttpResponseData> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/index/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], podcastName, Index, Unauthorised, ct);
    }

    [Function("PodcastGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], podcastName, Get, Unauthorised, ct);
    }

    [Function("PodcastPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        FunctionContext executionContext,
        [FromBody] Dtos.Podcast podcastChangeRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest), Post,
            Unauthorised, ct);
    }

    private async Task<HttpResponseData> Post(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper, 
        CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Post)} Podcast Change Request: episode-id: '{podcastChangeRequestWrapper.PodcastId}'. {JsonSerializer.Serialize(podcastChangeRequestWrapper.Podcast)}");
            var podcast = await podcastRepository.GetBy(x => x.Id == podcastChangeRequestWrapper.PodcastId);
            if (podcast == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                $"{nameof(Post)} Updating podcast-id '{podcastChangeRequestWrapper.PodcastId}'.");

            UpdatePodcast(podcast, podcastChangeRequestWrapper.Podcast);
            await podcastRepository.Update(podcast);
            if (podcastChangeRequestWrapper.Podcast.Removed.HasValue &&
                podcastChangeRequestWrapper.Podcast.Removed.Value)
            {
                foreach (var documentId in podcast.Episodes.Select(x => x.Id))
                {
                    try
                    {
                        var result = await searchClient.DeleteDocumentsAsync(
                            "id",
                            new[] {documentId.ToString()},
                            new IndexDocumentsOptions {ThrowOnAnyError = true},
                            c);
                        var success = result.Value.Results.First().Succeeded;
                        if (!success)
                        {
                            logger.LogError(result.Value.Results.First().ErrorMessage);
                        }
                        else
                        {
                            logger.LogInformation(
                                $"Removed episode from podcast with id '{podcast.Id}' with episode-id '{documentId}' from search-index.");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            $"Error removing episode from podcast with id '{podcast.Id}' with episode-id '{documentId}' from search-index.");
                    }
                }
            }

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to update podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update podcast"), c);
        return failure;
    }

    private void UpdatePodcast(RedditPodcastPoster.Models.Podcast podcast, Dtos.Podcast podcastChangeRequest)
    {
        if (podcastChangeRequest.Removed != null)
        {
            podcast.Removed = podcastChangeRequest.Removed;
        }

        if (podcastChangeRequest.IndexAllEpisodes != null)
        {
            podcast.IndexAllEpisodes = podcastChangeRequest.IndexAllEpisodes.Value;
        }

        if (podcastChangeRequest.BypassShortEpisodeChecking != null)
        {
            podcast.BypassShortEpisodeChecking = podcastChangeRequest.BypassShortEpisodeChecking.Value;
        }

        if (podcastChangeRequest.ReleaseAuthority != null)
        {
            podcast.ReleaseAuthority = podcastChangeRequest.ReleaseAuthority.Value;
        }

        if (podcastChangeRequest.PrimaryPostService != null)
        {
            podcast.PrimaryPostService = podcastChangeRequest.PrimaryPostService.Value;
        }

        if (podcastChangeRequest.AppleId != null)
        {
            podcast.AppleId = podcastChangeRequest.AppleId.Value;
        }

        if (podcastChangeRequest.YouTubePublishingDelayTimeSpan != null)
        {
            podcast.YouTubePublishingDelayTimeSpan = podcastChangeRequest.YouTubePublishingDelayTimeSpan;
        }

        if (podcastChangeRequest.SkipEnrichingFromYouTube != null)
        {
            podcast.SkipEnrichingFromYouTube = podcastChangeRequest.SkipEnrichingFromYouTube.Value;
        }

        if (podcastChangeRequest.TwitterHandle != null)
        {
            podcast.TwitterHandle = podcastChangeRequest.TwitterHandle;
        }

        if (podcastChangeRequest.TitleRegex != null)
        {
            podcast.TitleRegex = podcastChangeRequest.TitleRegex;
        }

        if (podcastChangeRequest.DescriptionRegex != null)
        {
            podcast.DescriptionRegex = podcastChangeRequest.DescriptionRegex;
        }

        if (podcastChangeRequest.EpisodeMatchRegex != null)
        {
            podcast.EpisodeMatchRegex = podcastChangeRequest.EpisodeMatchRegex;
        }

        if (podcastChangeRequest.EpisodeIncludeTitleRegex != null)
        {
            podcast.EpisodeIncludeTitleRegex = podcastChangeRequest.EpisodeIncludeTitleRegex;
        }

        if (podcastChangeRequest.DefaultSubject != null)
        {
            podcast.DefaultSubject = podcastChangeRequest.DefaultSubject;
        }
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, string podcastName, CancellationToken c)
    {
        try
        {
            logger.LogInformation($"{nameof(Index)} Index podcast '{podcastName}'.");

            var podcast = await podcastRepository.GetBy(x => x.Name == podcastName);
            if (podcast != null)
            {
                new Dtos.Podcast
                {
                    Id = podcast.Id,
                    Removed = podcast.Removed,
                    IndexAllEpisodes = podcast.IndexAllEpisodes,
                    BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
                    ReleaseAuthority = podcast.ReleaseAuthority,
                    PrimaryPostService = podcast.PrimaryPostService,
                    SpotifyId = podcast.SpotifyId,
                    AppleId = podcast.AppleId,
                    YouTubePublishingDelayTimeSpan = podcast.YouTubePublishingDelayTimeSpan,
                    SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
                    TwitterHandle = podcast.TwitterHandle,
                    TitleRegex = podcast.TitleRegex,
                    DescriptionRegex = podcast.DescriptionRegex,
                    EpisodeMatchRegex = podcast.EpisodeMatchRegex,
                    EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
                    DefaultSubject = podcast.DefaultSubject
                };
                return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(podcast, c);
            }

            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Index)}: Failed to index-podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c);
        return failure;
    }

    private async Task<HttpResponseData> Index(HttpRequestData req, string podcastName, CancellationToken c)
    {
        try
        {
            logger.LogInformation($"{nameof(Index)} Index podcast '{podcastName}'.");

            if (_indexerOptions.ReleasedDaysAgo == null)
            {
                throw new InvalidOperationException("Unable to index with null released-days-ago.");
            }

            var indexingContext = _indexerOptions.ToIndexingContext() with
            {
                IndexSpotify = true,
                SkipSpotifyUrlResolving = false,
                SkipYouTubeUrlResolving = false,
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false,
                SkipPodcastDiscovery = true
            };

            await indexer.Index(podcastName, indexingContext);
            await searchIndexerService.RunIndexer();

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Index)}: Failed to index-podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c);
        return failure;
    }
}