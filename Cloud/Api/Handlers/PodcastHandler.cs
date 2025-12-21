using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.CloudflareRedirect;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Podcast = Api.Dtos.Podcast;
using PodcastRenameRequest = Api.Models.PodcastRenameRequest;

namespace Api.Handlers;

public class PodcastHandler(
    IIndexer indexer,
    IEpisodeSearchIndexerService searchIndexerService,
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    IRedirectService redirectService,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<PodcastHandler> logger) : IPodcastHandler
{
    private const int MaxPodcastToRename = 2;
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public async Task<HttpResponseData> Post(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                "{method}: Podcast Change Request: episode-id: '{podcastId}'. {podcastChangeRequestWrapper}",
                nameof(Post), podcastChangeRequestWrapper.PodcastId,
                JsonSerializer.Serialize(podcastChangeRequestWrapper.Podcast));
            var podcast = await podcastRepository.GetBy(x => x.Id == podcastChangeRequestWrapper.PodcastId);
            if (podcast == null)
            {
                logger.LogWarning("{method}: Podcast with id '{podcastId}' not found.", nameof(Post),
                    podcastChangeRequestWrapper.PodcastId);
                return await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { id = podcastChangeRequestWrapper.PodcastId }, c);
            }

            logger.LogInformation("{method}: Updating podcast-id '{podcastId}'.", nameof(Post),
                podcastChangeRequestWrapper.PodcastId);

            UpdatePodcast(podcast, podcastChangeRequestWrapper.Podcast);
            if (podcastChangeRequestWrapper.AllowNameChange)
            {
                await UpdateName(podcast, podcastChangeRequestWrapper.Podcast.Name);
            }

            await podcastRepository.Save(podcast);

            if (podcastChangeRequestWrapper.Podcast.Removed.HasValue &&
                podcastChangeRequestWrapper.Podcast.Removed.Value)
            {
                await DeleteEpisodesFromSearchIndex(c, podcast);
            }
            else if (podcastChangeRequestWrapper.AllowNameChange)
            {
                if (podcast.Episodes.Any())
                {
                    await searchIndexerService.IndexEpisodes(podcast.Episodes.Select(x => x.Id), c);
                }
            }

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update podcast.", nameof(Post));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update podcast"), c);
        return failure;
    }

    public async Task<HttpResponseData> Get(HttpRequestData req, PodcastGetRequest podcastGetRequest, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{method}: Get podcast with name '{podcastName}'.", nameof(Get), podcastGetRequest.PodcastName);
            var podcastResult = await GetPodcast(podcastGetRequest, c);
            if (podcastResult is { RetrievalState: PodcastRetrievalState.Found, Podcast: not null })
            {
                var podcast = podcastResult.Podcast;
                var dto = new Podcast
                {
                    Id = podcast.Id,
                    Name = podcast.Name,
                    Language = podcast.Language,
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
                    BlueskyHandle = podcast.BlueskyHandle ?? string.Empty,
                    TitleRegex = podcast.TitleRegex,
                    DescriptionRegex = podcast.DescriptionRegex,
                    EpisodeMatchRegex = podcast.EpisodeMatchRegex,
                    EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
                    DefaultSubject = podcast.DefaultSubject,
                    IgnoreAllEpisodes = podcast.IgnoreAllEpisodes ?? false,
                    YouTubeChannelId = podcast.YouTubeChannelId,
                    YouTubePlaylistId = podcast.YouTubePlaylistId,
                    IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
                    IgnoredSubjects = podcast.IgnoredSubjects
                };
                return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(dto, c);
            }

            if (podcastResult.RetrievalState == PodcastRetrievalState.NotFound)
            {
                logger.LogError("Unable to find podcast with name '{name}' and episode-id '{episodeId}'.", podcastGetRequest.PodcastName, podcastGetRequest.EpisodeId);
                return await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c);
            }

            if (podcastResult.RetrievalState == PodcastRetrievalState.Conflict)
            {
                logger.LogError("Multiple podcasts with name '{name}' and episode-id '{episodeId}'.", podcastGetRequest.PodcastName, podcastGetRequest.EpisodeId);
                return await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(SubmitUrlResponse.Failure("Multiple podcasts found"), c);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to index-podcast.", nameof(Get));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c);
        return failure;
    }

    public async Task<HttpResponseData> Index(HttpRequestData req, string podcastName, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{method}: Index podcast '{podcastName}'.", nameof(Index), podcastName);
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
            var indexed = SearchIndexerState.Unknown;
            if (response.IndexStatus == IndexStatus.Performed)
            {
                var episodes = response.UpdatedEpisodes != null
                    ? response.UpdatedEpisodes.Select(x => x.EpisodeId)
                    : [];
                var result = await searchIndexerService.IndexEpisodes(episodes, c);
                indexed = result.ToDto();
            }

            var status = response.IndexStatus switch
            {
                IndexStatus.NotFound => HttpStatusCode.NotFound,
                IndexStatus.Performed => HttpStatusCode.OK,
                _ => HttpStatusCode.BadRequest
            };

            if (status == HttpStatusCode.NotFound)
            {
                logger.LogWarning("{method}: Podcast with name '{podcastName}' not found.", nameof(Index), podcastName);
            }

            return await req.CreateResponse(status).WithJsonBody(IndexPodcastResponse.ToDto(response, indexed), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to index-podcast.", nameof(Index));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c);
        return failure;
    }

    public async Task<HttpResponseData> Rename(HttpRequestData req, PodcastRenameRequest change, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            if (change.NewName.Contains("/"))
            {
                logger.LogError("New podcast-name contains invalid-character: '{NewName}'.", change.NewName);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            logger.LogInformation(
                "{method}: Podcast Name-Change Request: podcast-name: '{name}'. new-name: '{newName}'.",
                nameof(Post), change.Name, change.NewName);
            var podcasts = await podcastRepository.GetAllBy(x =>
                    x.Name.ToLower() == change.Name.ToLower() || x.Name.ToLower() == change.NewName.ToLower())
                .ToListAsync(c);
            if (podcasts.Any(x => x.Name.ToLower() == change.NewName.ToLower()))
            {
                logger.LogError("Podcast found with new-name '{name}'.", change.Name);
                return req.CreateResponse(HttpStatusCode.Conflict);
            }

            if (podcasts.All(x => x.Name != change.Name))
            {
                logger.LogError("Podcast not found with name '{name}'.", change.Name);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var podcastsToUpdate = podcasts.Where(x => x.Name == change.Name).ToArray();
            if (podcastsToUpdate.Length > MaxPodcastToRename)
            {
                logger.LogError(
                    "Operation to rename podcasts with name '{name}' to '{newName}' impacts {podcastsToUpdateCount} podcasts. Operation rejected.",
                    change.Name, change.NewName, podcastsToUpdate.Length);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            var result = await redirectService.CreatePodcastRedirect(new PodcastRedirect(change.Name, change.NewName));
            logger.LogInformation("Result of {method} = {result}", nameof(redirectService.CreatePodcastRedirect),
                result);
            if (result)
            {
                var episodeIds = new List<Guid>();
                foreach (var podcast in podcastsToUpdate)
                {
                    var oldName = podcast.Name;
                    podcast.Name = change.NewName;
                    await podcastRepository.Save(podcast);
                    logger.LogInformation("Renamed podcast '{oldName}' to '{newName}'.", oldName, change.NewName);
                    episodeIds.AddRange(podcast.Episodes.Select(x => x.Id));
                }

                var indexed = await searchIndexerService.IndexEpisodes(episodeIds.Distinct(), c);
                var indexState = indexed.ToDto();

                logger.LogInformation("Search-index run-state: {indexState}.", indexState);
                return await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(new { indexState = indexState }, c);
            }

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to rename podcast.", nameof(Post));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to rename podcast"), c);
        return failure;
    }

    private async Task UpdateName(RedditPodcastPoster.Models.Podcast podcast, string? podcastName)
    {
        if (podcast.Episodes.Count > 1)
        {
            throw new InvalidOperationException(
                "Cannot rename a podcast with more than one episode. Use rename-endpoint");
        }

        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new InvalidOperationException(
                "Supplied podcast-name is null/empty");
        }

        var sameNamePodcasts =
            await podcastRepository.GetAllBy(x => x.Name == podcastName && x.Id != podcast.Id, x => new { id = x.Id })
                .ToListAsync();
        if (sameNamePodcasts.Count > 0)
        {
            throw new InvalidOperationException(
                $"Other podcasts have requested name '{podcastName}'. Podcast-ids: {string.Join(", ", sameNamePodcasts.Select(x => $"'{x.id}'"))}.");
        }

        podcast.Name = podcastName.Trim();
        podcast.FileKey = FileKeyFactory.GetFileKey(podcast.Name);
    }

    private async Task<bool> DeleteEpisodesFromSearchIndex(CancellationToken c,
        RedditPodcastPoster.Models.Podcast podcast)
    {
        var episodeIds = podcast.Episodes.Select(x => x.Id.ToString()).ToArray();
        var result = await searchClient.DeleteDocumentsAsync("id", episodeIds,
            new IndexDocumentsOptions { ThrowOnAnyError = false }, c);
        var failure = result.Value.Results.Any(x => !x.Succeeded);
        if (failure)
        {
            logger.LogError(
                "Removed {successCount} documents. Failed to remove {failureCount} documents with search-index with ids: {documentIds}.",
                result.Value.Results.Count(x => x.Succeeded),
                result.Value.Results.Count(x => !x.Succeeded),
                string.Join(",", episodeIds.Select(x => $"'{x}'")));
        }
        else
        {
            logger.LogInformation("Removed {successCount} documents. ", result.Value.Results.Count(x => x.Succeeded));
        }

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

        if (podcastChangeRequest.SpotifyId != null)
        {
            podcast.SpotifyId = podcastChangeRequest.SpotifyId == string.Empty ? string.Empty : podcastChangeRequest.SpotifyId;
        }

        if (podcastChangeRequest.AppleId != null ||
            (podcastChangeRequest.NullAppleId.HasValue && podcastChangeRequest.NullAppleId.Value))
        {
            if (podcastChangeRequest.NullAppleId.HasValue && podcastChangeRequest.NullAppleId.Value)
            {
                podcast.AppleId = null;
            }
            else if (podcastChangeRequest.AppleId.HasValue)
            {
                podcast.AppleId = podcastChangeRequest.AppleId.Value;
            }
            else
            {
                throw new InvalidOperationException("Indeterminate state of apple-id");
            }
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

        if (podcastChangeRequest.YouTubePlaylistId != null)
        {
            podcast.YouTubePlaylistId = podcastChangeRequest.YouTubePlaylistId;
        }

        if (podcastChangeRequest.SkipEnrichingFromYouTube != null)
        {
            podcast.SkipEnrichingFromYouTube = podcastChangeRequest.SkipEnrichingFromYouTube.Value;
        }

        if (podcastChangeRequest.TwitterHandle != null)
        {
            podcast.TwitterHandle = podcastChangeRequest.TwitterHandle;
        }

        if (podcastChangeRequest.BlueskyHandle != null)
        {
            podcast.BlueskyHandle = string.IsNullOrWhiteSpace(podcastChangeRequest.BlueskyHandle)
                ? null
                : podcastChangeRequest.BlueskyHandle;
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
            podcast.IndexAllEpisodes = false;
        }

        if (podcastChangeRequest.DefaultSubject != null)
        {
            podcast.DefaultSubject = podcastChangeRequest.DefaultSubject == string.Empty ? null : podcastChangeRequest.DefaultSubject;
        }

        if (podcastChangeRequest.IgnoreAllEpisodes != null)
        {
            if (podcastChangeRequest.IgnoreAllEpisodes.HasValue && podcastChangeRequest.IgnoreAllEpisodes.Value)
            {
                podcast.IgnoreAllEpisodes = true;
            }
            else
            {
                podcast.IgnoreAllEpisodes = null;
            }
        }

        if (podcastChangeRequest.IgnoredSubjects != null)
        {
            podcast.IgnoredSubjects = podcastChangeRequest.IgnoredSubjects;
        }

        if (podcastChangeRequest.IgnoredAssociatedSubjects != null)
        {
            podcast.IgnoredAssociatedSubjects = podcastChangeRequest.IgnoredAssociatedSubjects;
        }

        if (podcastChangeRequest.Language != null)
        {
            podcast.Language = podcastChangeRequest.Language == string.Empty ? null : podcastChangeRequest.Language;
        }
    }

    private async Task<PodcastWrapper> GetPodcast(PodcastGetRequest podcastGetRequest, CancellationToken c)
    {
        var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastGetRequest.PodcastName).ToListAsync(c);
        if (!podcasts.Any())
        {
            return new PodcastWrapper(null, PodcastRetrievalState.NotFound);
        }

        if (podcasts.Count == 1)
        {
            return new PodcastWrapper(podcasts.Single(), PodcastRetrievalState.Found);
        }

        if (podcasts.Count > 1)
        {
            var indexedPodcasts = podcasts.Where(x => x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex)).ToArray();
            if (indexedPodcasts.Length == 1)
            {
                return new PodcastWrapper(indexedPodcasts.Single(), PodcastRetrievalState.Found);
            }
            if (podcastGetRequest.EpisodeId != null && indexedPodcasts.Length > 1 && podcastGetRequest.EpisodeId.HasValue)
            {
                var podcastByEpisode = podcasts.Where(x=>x.Episodes.Any(x=>x.Id== podcastGetRequest.EpisodeId));
                if (podcastByEpisode.Count()==1)
                {
                    return new PodcastWrapper(podcastByEpisode.Single(), PodcastRetrievalState.Found);
                }
            }
        }

        return new PodcastWrapper(null, PodcastRetrievalState.Conflict);
    }
}