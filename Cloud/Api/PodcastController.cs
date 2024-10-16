using System.Net;
using System.Text.Json;
using Api.Dtos;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.CloudflareRedirect;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;

namespace Api;

public class PodcastController(
    IIndexer indexer,
    ISearchIndexerService searchIndexerService,
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    IRedirectService redirectService,
    ILogger<PodcastController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<IndexerOptions> indexerOptions,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
{
    private const int MaxPodcastToRename = 2;
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;


    [Function("PodcastRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/name/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        [FromBody] Dtos.PodcastRenameRequest newPodcastName,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        var podcastRenameRequest = new PodcastRenameRequest(podcastName, newPodcastName.NewPodcastName);
        return HandleRequest(req, ["admin"], podcastRenameRequest, Rename, Unauthorised, ct);
    }

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
        [FromBody] Podcast podcastChangeRequest,
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
                $"{nameof(Post)}: Podcast Change Request: episode-id: '{podcastChangeRequestWrapper.PodcastId}'. {JsonSerializer.Serialize(podcastChangeRequestWrapper.Podcast)}");
            var podcast = await podcastRepository.GetBy(x => x.Id == podcastChangeRequestWrapper.PodcastId);
            if (podcast == null)
            {
                logger.LogWarning(
                    $"{nameof(Post)}: Podcast with id '{podcastChangeRequestWrapper.PodcastId}' not found.");
                return await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new {id = podcastChangeRequestWrapper.PodcastId}, c);
            }

            logger.LogInformation(
                $"{nameof(Post)}: Updating podcast-id '{podcastChangeRequestWrapper.PodcastId}'.");

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
                            logger.LogError(
                                $"{nameof(Post)}: Failure to delete search-document with id '{documentId}'.");
                            logger.LogError(result.Value.Results.First().ErrorMessage);
                        }
                        else
                        {
                            logger.LogInformation(
                                $"{nameof(Post)}: Removed episode from podcast with id '{podcast.Id}' with episode-id '{documentId}' from search-index.");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            $"{nameof(Post)}: Error removing episode from podcast with id '{podcast.Id}' with episode-id '{documentId}' from search-index.");
                    }
                }
            }

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Post)}: Failed to update podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update podcast"), c);
        return failure;
    }

    private void UpdatePodcast(RedditPodcastPoster.Models.Podcast podcast, Podcast podcastChangeRequest)
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

        if (podcastChangeRequest.UnsetReleaseAuthority != null && podcastChangeRequest.UnsetReleaseAuthority.Value)
        {
            podcast.ReleaseAuthority = null;
        }

        if (podcastChangeRequest.PrimaryPostService != null)
        {
            podcast.PrimaryPostService = podcastChangeRequest.PrimaryPostService.Value;
        }

        if (podcastChangeRequest.UnsetPrimaryPostService != null && podcastChangeRequest.UnsetPrimaryPostService.Value)
        {
            podcast.PrimaryPostService = null;
        }

        if (podcastChangeRequest.AppleId != null)
        {
            podcast.AppleId = podcastChangeRequest.AppleId.Value;
        }

        if (podcastChangeRequest.YouTubePublishingDelayTimeSpan != null)
        {
            if (podcastChangeRequest.YouTubePublishingDelayTimeSpan == string.Empty)
            {
                podcast.YouTubePublicationOffset = null;
            }
            else
            {
                podcast.YouTubePublicationOffset =
                    TimeSpan.Parse(podcastChangeRequest.YouTubePublishingDelayTimeSpan).Ticks;
            }
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
            logger.LogInformation($"{nameof(Get)}: Get podcast with name '{podcastName}'.");
            podcastName = WebUtility.UrlDecode(podcastName);
            var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastName).ToListAsync(c);
            var podcast = GetPodcast(podcasts);
            if (podcast != null)
            {
                var dto = new Podcast
                {
                    Id = podcast.Id,
                    Removed = podcast.Removed,
                    IndexAllEpisodes = podcast.IndexAllEpisodes,
                    BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
                    ReleaseAuthority = podcast.ReleaseAuthority,
                    PrimaryPostService = podcast.PrimaryPostService,
                    SpotifyId = podcast.SpotifyId,
                    AppleId = podcast.AppleId,
                    YouTubePublishingDelayTimeSpan = podcast.YouTubePublicationOffset.HasValue
                        ? TimeSpan.FromTicks(podcast.YouTubePublicationOffset.Value).ToString("g")
                        : string.Empty,
                    SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
                    TwitterHandle = podcast.TwitterHandle,
                    TitleRegex = podcast.TitleRegex,
                    DescriptionRegex = podcast.DescriptionRegex,
                    EpisodeMatchRegex = podcast.EpisodeMatchRegex,
                    EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
                    DefaultSubject = podcast.DefaultSubject
                };
                return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(dto, c);
            }

            logger.LogWarning($"{nameof(Get)}: Podcast with name '{podcastName}' not found.");
            return await req.CreateResponse(HttpStatusCode.NotFound).WithJsonBody(new {name = podcastName}, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to index-podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c);
        return failure;
    }

    private static RedditPodcastPoster.Models.Podcast? GetPodcast(List<RedditPodcastPoster.Models.Podcast> podcasts)
    {
        RedditPodcastPoster.Models.Podcast? podcast = null;
        if (podcasts.Count() == 1)
        {
            podcast = podcasts.Single();
        }
        else if (podcasts.Count() > 1)
        {
            podcast = podcasts.SingleOrDefault(x => x.IndexAllEpisodes);
        }

        return podcast;
    }

    private async Task<HttpResponseData> Index(HttpRequestData req, string podcastName, CancellationToken c)
    {
        try
        {
            logger.LogInformation($"{nameof(Index)}: Index podcast '{podcastName}'.");
            podcastName = WebUtility.UrlDecode(podcastName);

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

            var response = await indexer.Index(podcastName, indexingContext);
            if (response.IndexStatus == IndexStatus.Performed)
            {
                await searchIndexerService.RunIndexer();
            }

            var status = response.IndexStatus switch
            {
                IndexStatus.NotFound => HttpStatusCode.NotFound,
                IndexStatus.Performed => HttpStatusCode.OK,
                _ => HttpStatusCode.BadRequest
            };

            if (status == HttpStatusCode.NotFound)
            {
                logger.LogWarning($"{nameof(Index)}: Podcast with name '{podcastName}' not found.");
            }

            return await req.CreateResponse(status).WithJsonBody(new {episodeIds = response.UpdatedEpisodeIds}, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Index)}: Failed to index-podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c);
        return failure;
    }

    private async Task<HttpResponseData> Rename(HttpRequestData req, PodcastRenameRequest change, CancellationToken c)
    {
        try
        {
            if (change.NewName.Contains("/"))
            {
                logger.LogError($"New podcast-name contains invalid-character: '{change.NewName}'.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            logger.LogInformation(
                $"{nameof(Post)}: Podcast Name-Change Request: podcast-name: '{change.Name}'. new-name: '{change.NewName}'.");
            var podcasts = await podcastRepository.GetAllBy(x =>
                    x.Name.ToLower() == change.Name.ToLower() || x.Name.ToLower() == change.NewName.ToLower())
                .ToListAsync(c);
            if (podcasts.Any(x => x.Name.ToLower() == change.NewName.ToLower()))
            {
                logger.LogError($"Podcast found with new-name '{change.Name}'.");
                return req.CreateResponse(HttpStatusCode.Conflict);
            }

            if (podcasts.All(x => x.Name != change.Name))
            {
                logger.LogError($"Podcast not found with name '{change.Name}'.");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var podcastsToUpdate = podcasts.Where(x => x.Name == change.Name);
            if (podcastsToUpdate.Count() > MaxPodcastToRename)
            {
                logger.LogError(
                    $"Operation to rename podcasts with name '{change.Name}' to '{change.NewName}' impacts {podcastsToUpdate.Count()} podcasts. Operation rejected.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            var result =
                await redirectService.CreatePodcastRedirect(
                    new PodcastRedirect(
                        change.Name,
                        change.NewName));
            if (result)
            {
                foreach (var podcast in podcastsToUpdate)
                {
                    podcast.Name = change.NewName;
                    await podcastRepository.Save(podcast);
                }

                var indexState = await searchIndexerService.RunIndexer();
                return await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(new {indexState = indexState.ToString()}, c);
            }

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Post)}: Failed to rename podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to rename podcast"), c);
        return failure;
    }
}