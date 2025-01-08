using System.Net;
using System.Text.Json;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Azure.Search.Documents;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening;

namespace Api;

public class EpisodeController(
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IContentPublisher contentPublisher,
    IPostManager postManager,
    ITweetManager tweetManager,
    IBlueskyPoster blueskyPoster,
    IClientPrincipalFactory clientPrincipalFactory,
    IShortnerService shortnerService,
    IImageUpdater imageUpdater,
    ILogger<EpisodeController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    private const string? Route = "episode/{episodeId:guid}";

    [Function("EpisodeGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], episodeId, Get, Unauthorised, ct);
    }

    [Function("OutgoingEpisodesGet")]
    public Task<HttpResponseData> GetOutgoing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "episodes/outgoing")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], GetOutgoing, Unauthorised, ct);
    }

    [Function("EpisodePost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody] EpisodeChangeRequest episodeChangeRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], new EpisodeChangeRequestWrapper(episodeId, episodeChangeRequest), Post,
            Unauthorised, ct);
    }

    [Function("EpisodePublish")]
    public Task<HttpResponseData> EpisodePublish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "episode/publish/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody] EpisodePublishRequest episodePostRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], new EpisodePublishRequestWrapper(episodeId, episodePostRequest), Publish,
            Unauthorised, ct);
    }

    [Function("EpisodeDelete")]
    public Task<HttpResponseData> EpisodeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "episode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["admin"], episodeId, Delete, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Delete(HttpRequestData req, Guid episodeId, ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            var podcasts = await podcastRepository
                .GetAllBy(x => x.Episodes.Any(x => x.Id == episodeId))
                .ToListAsync(c);
            if (podcasts.Count > 1)
            {
                var tooManyPodcasts = await req
                    .CreateResponse(HttpStatusCode.Ambiguous)
                    .WithJsonBody(new { message = $"Multiple podcasts. Count='{podcasts.Count}'." }, c);
                return tooManyPodcasts;
            }

            if (podcasts.Count == 0)
            {
                var notFound = req
                    .CreateResponse(HttpStatusCode.NotFound);
                return notFound;
            }

            var count = podcasts.Single().Episodes.Count(x => x.Id == episodeId);
            if (count > 1)
            {
                var tooManyEpisodes = await req
                    .CreateResponse(HttpStatusCode.Ambiguous)
                    .WithJsonBody(
                        new
                        {
                            message =
                                $"Podcast with id '{podcasts.Single().Id}' has multiple episodes with id. Count='{count}'."
                        }, c);
                return tooManyEpisodes;
            }

            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Podcast with id '{podcasts.Single().Id}' expected to have episode with id '{episodeId}' but not found.");
            }

            var episode = podcasts.Single().Episodes.Single(x => x.Id == episodeId);

            if (episode.Tweeted || episode.Posted)
            {
                return await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(
                    new { message = "Cannot remove episode.", posted = episode.Posted, tweeted = episode.Tweeted }, c);
            }

            var removed = podcasts.Single().Episodes.Remove(episode);
            if (!removed)
            {
                throw new InvalidOperationException(
                    $"Unable to remove episode from Podcast with id '{podcasts.Single().Id}' episode with id '{episodeId}'.");
            }

            await DeleteSearchEntry(podcasts.Single().Name, episodeId, cp, c);

            logger.LogWarning(
                $"Delete episode from podcast with id '{podcasts.Single().Id}' and episode-id '{episodeId}'");
            await podcastRepository.Save(podcasts.Single());
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Delete)}: Failed to delete episode.");
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }


    private async Task<HttpResponseData> Publish(HttpRequestData req, EpisodePublishRequestWrapper publishRequest,
        ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var response = new EpisodePublishResponse();
            var podcast = await podcastRepository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(ep => ep.Id == publishRequest.EpisodeId));
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with episode-id '{publishRequest.EpisodeId}' not found.");
            }

            var episode = podcast!.Episodes.Single(x => x.Id == publishRequest.EpisodeId);

            var podcastEpisode = new RedditPodcastPoster.Models.PodcastEpisode(podcast, episode);

            if (publishRequest.EpisodePublishRequest.Post)
            {
                var result = await podcastEpisodePoster.PostPodcastEpisode(podcastEpisode);
                if (!result.Success)
                {
                    logger.LogError(result.ToString());
                }

                response.Posted = result.Success;
            }

            if (publishRequest.EpisodePublishRequest.Tweet || publishRequest.EpisodePublishRequest.BlueskyPost)
            {
                var shortnerResult = await shortnerService.Write(podcastEpisode);
                if (!shortnerResult.Success)
                {
                    logger.LogError("Unsuccessful shortening-url.");
                }

                if (publishRequest.EpisodePublishRequest.Tweet)
                {
                    try
                    {
                        var result = await tweetPoster.PostTweet(podcastEpisode, shortnerResult.Url);
                        if (result != TweetSendStatus.Sent)
                        {
                            logger.LogError($"Tweet result: '{result}'.");
                        }
                        else
                        {
                            response.Tweeted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e,
                            "Failed to tweet for podcast-id: {podcastId} and episode-id {episodeId}.",
                            podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                    }
                }

                if (publishRequest.EpisodePublishRequest.BlueskyPost)
                {
                    try
                    {
                        var result = await blueskyPoster.Post(podcastEpisode, shortnerResult.Url);
                        if (result != BlueskySendStatus.Success)
                        {
                            logger.LogError($"Bluesky-post result: '{result}'.");
                        }
                        else
                        {
                            response.BlueskyPosted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e,
                            "Failed to bluesky-post for podcast-id: {podcastId} and episode-id {episodeId}.",
                            podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                    }
                }
            }

            if (response.Updated())
            {
                if (episode.Ignored)
                {
                    episode.Ignored = false;
                }

                if (episode.Removed)
                {
                    episode.Removed = false;
                }

                await podcastRepository.Save(podcast);
            }

            await contentPublisher.PublishHomepage();

            var success = await req.CreateResponse(
                response.Updated() ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest
            ).WithJsonBody(response, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Publish)}: Failed to publish episode.");
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }

    private async Task<HttpResponseData> GetOutgoing(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var (days, posted, tweeted, blueskyPosted) = ParseOutgoingQuery(req);

            var episodes = new List<DiscreteEpisode>();
            var since = DateTimeExtensions.DaysAgo(days);
            var podcastIds = await podcastRepository.GetAllBy(x =>
                    (
                        !x.Removed.IsDefined() ||
                        x.Removed == false
                    ) &&
                    x.Episodes.Any(ep =>
                        ep.Release > since &&
                        (!ep.Posted || posted) &&
                        (!ep.Tweeted || tweeted) &&
                        (!(ep.BlueskyPosted.IsDefined() && ep.BlueskyPosted == true) || blueskyPosted)),
                x => new { guid = x.Id }).ToListAsync(c);
            foreach (var podcastId in podcastIds)
            {
                var podcast = await podcastRepository.GetBy(x => x.Id == podcastId.guid);
                var unpostedEpisodes =
                    podcast!.Episodes.Where(x =>
                            x.Release > since &&
                            (!x.Posted || posted) &&
                            (!x.Tweeted || tweeted) &&
                            (!(x.BlueskyPosted.HasValue && x.BlueskyPosted.Value) || blueskyPosted))
                        .Select(x => x.Enrich(podcast));
                episodes.AddRange(unpostedEpisodes);
            }

            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(episodes.OrderByDescending(x => x.Release), c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetOutgoing)}: Failed to get out-going episodes.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c);
        return failure;
    }

    private (int days, bool posted, bool tweeted, bool blueskyPosted) ParseOutgoingQuery(HttpRequestData req)
    {
        if (!bool.TryParse(req.Query["tweeted"], out var tweeted))
        {
            tweeted = false;
        }

        if (!bool.TryParse(req.Query["posted"], out var posted))
        {
            posted = false;
        }

        if (!bool.TryParse(req.Query["blueskyPosted"], out var blueskyPosted))
        {
            blueskyPosted = false;
        }

        if (!int.TryParse(req.Query["days"], out var days))
        {
            days = 7;
        }

        if (days > 14)
        {
            days = 14;
        }

        return (days, posted, tweeted, blueskyPosted);
    }

    private async Task<HttpResponseData> Post(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Post)} Episode Change Request: episode-id: '{episodeChangeRequestWrapper.EpisodeId}'. {JsonSerializer.Serialize(episodeChangeRequestWrapper.EpisodeChangeRequest)}");
            var podcast = await podcastRepository.GetBy(x =>
                x.Episodes.Any(ep => ep.Id == episodeChangeRequestWrapper.EpisodeId));
            var episode = podcast?.Episodes.SingleOrDefault(x => x.Id == episodeChangeRequestWrapper.EpisodeId);
            if (episode == null)
            {
                logger.LogWarning($"Episode with id '{episodeChangeRequestWrapper.EpisodeId}' not found.");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                $"{nameof(Post)} Updating episode-id '{episodeChangeRequestWrapper.EpisodeId}' of podcast with id '{podcast.Id}'. Original-episode: {JsonSerializer.Serialize(episode)}");

            var changeState = UpdateEpisode(episode, episodeChangeRequestWrapper.EpisodeChangeRequest);

            var indexingContext = new IndexingContext();
            if (changeState.UpdateImages)
            {
                await imageUpdater.UpdateImages(
                    podcast,
                    episode,
                    new EpisodeImageUpdateRequest(
                        changeState.UpdateSpotifyImage,
                        changeState.UpdateAppleImage,
                        changeState.UpdateYouTubeImage,
                        changeState.UpdateBBCImage),
                    indexingContext);
            }

            await podcastRepository.Update(podcast);

            if (episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.HasValue &&
                episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.Value)
            {
                await DeleteSearchEntry(podcast.Name, episodeChangeRequestWrapper.EpisodeId, cp, c);
            }

            if (changeState.UnPost)
            {
                await postManager.RemoveEpisodePost(new RedditPodcastPoster.Models.PodcastEpisode(podcast, episode));
            }
            else if (changeState.UpdatedSubjects)
            {
                await postManager.UpdateFlare(new RedditPodcastPoster.Models.PodcastEpisode(podcast, episode));
            }

            var removeTweetResult = RemoveTweetState.Unknown;
            if (changeState.UnTweet)
            {
                try
                {
                    removeTweetResult = await tweetManager.RemoveTweet(new RedditPodcastPoster.Models.PodcastEpisode(podcast, episode));
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        $"Error using tweet-manager to remove tweet for episode with id '{episode.Id}'.");
                    removeTweetResult = RemoveTweetState.Other;
                }
            }

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            if (changeState.UnTweet)
            {
                response = await response.WithJsonBody(
                    new { TweetDeleted = removeTweetResult == RemoveTweetState.Deleted }, c);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Post)}: Failed to update episode.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c);
        return failure;
    }

    private async Task DeleteSearchEntry(
        string podcastName,
        Guid episodeId,
        ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                [episodeId.ToString()],
                new IndexDocumentsOptions { ThrowOnAnyError = true },
                c);
            var success = result.Value.Results.First().Succeeded;
            if (!success)
            {
                logger.LogError(result.Value.Results.First().ErrorMessage);
            }
            else
            {
                logger.LogInformation(
                    $"Removed episode from podcast with id '{podcastName}' with episode-id '{episodeId}' from search-index.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Error removing episode from podcast with id '{podcastName}' with episode-id '{episodeId}' from search-index.");
        }
    }

    private EpisodeChangeState UpdateEpisode(Episode episode,
        EpisodeChangeRequest episodeChangeRequest)
    {
        var changeState = new EpisodeChangeState();
        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Title))
        {
            episode.Title = episodeChangeRequest.Title;
        }

        if (episodeChangeRequest.Description != null)
        {
            episode.Description = episodeChangeRequest.Description;
        }

        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Duration))
        {
            episode.Length = TimeSpan.Parse(episodeChangeRequest.Duration);
        }

        if (episodeChangeRequest.SearchTerms != null)
        {
            episode.SearchTerms = episodeChangeRequest.SearchTerms;
        }

        if (episodeChangeRequest.Release != null)
        {
            episode.Release = episodeChangeRequest.Release.Value;
        }

        if (episodeChangeRequest.Explicit != null)
        {
            episode.Explicit = episodeChangeRequest.Explicit.Value;
        }

        if (episodeChangeRequest.Ignored != null)
        {
            episode.Ignored = episodeChangeRequest.Ignored.Value;
        }

        if (episodeChangeRequest.Posted != null)
        {
            if (!episodeChangeRequest.Posted.Value && episode.Posted)
            {
                changeState.UnPost = true;
            }

            episode.Posted = episodeChangeRequest.Posted.Value;
        }

        if (episodeChangeRequest.Removed != null)
        {
            episode.Removed = episodeChangeRequest.Removed.Value;
        }

        if (episodeChangeRequest.Tweeted != null)
        {
            if (!episodeChangeRequest.Tweeted.Value && episode.Tweeted)
            {
                changeState.UnTweet = true;
            }

            episode.Tweeted = episodeChangeRequest.Tweeted.Value;
        }

        if (episodeChangeRequest.BlueskyPosted != null)
        {
            if (!episodeChangeRequest.BlueskyPosted.Value && episode.BlueskyPosted.HasValue &&
                episode.BlueskyPosted.Value)
            {
                changeState.UnBlueskyPost = true;
            }

            episode.BlueskyPosted =
                episodeChangeRequest.BlueskyPosted.HasValue && episodeChangeRequest.BlueskyPosted.Value ? true : null;
        }

        if (episodeChangeRequest.Subjects != null && !episode.Subjects.SequenceEqual(episodeChangeRequest.Subjects))
        {
            changeState.UpdatedSubjects = true;
            episode.Subjects = episodeChangeRequest.Subjects.ToList();
        }

        if (episodeChangeRequest.Urls?.Spotify != null)
        {
            if (episodeChangeRequest.Urls.Spotify.ToString() == string.Empty)
            {
                episode.SpotifyId = string.Empty;
                episode.Urls.Spotify = null;
                if (episode.Images != null)
                {
                    episode.Images.YouTube = null;
                }
            }
            else
            {
                if (SpotifyPodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Spotify))
                {
                    var spotifyId = SpotifyIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Spotify);
                    if (!string.IsNullOrWhiteSpace(spotifyId))
                    {
                        episode.SpotifyId = spotifyId;
                        episode.Urls.Spotify = episodeChangeRequest.Urls.Spotify.CleanSpotifyUrl();
                        changeState.UpdateSpotifyImage = true;
                    }
                }
                else
                {
                    logger.LogError($"Invalid spotify-url: '{episodeChangeRequest.Urls.Spotify}'.");
                }
            }
        }

        if (episodeChangeRequest.Urls?.Apple != null)
        {
            if (episodeChangeRequest.Urls.Apple.ToString() == string.Empty)
            {
                episode.AppleId = null;
                episode.Urls.Apple = null;
                if (episode.Images != null)
                {
                    episode.Images.Apple = null;
                }
            }
            else
            {
                if (ApplePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Apple))
                {
                    var appleId = AppleIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Apple);
                    if (appleId != null)
                    {
                        episode.AppleId = appleId;
                        episode.Urls.Apple = episodeChangeRequest.Urls.Apple.CleanAppleUrl();
                        changeState.UpdateAppleImage = true;
                    }
                }
                else
                {
                    logger.LogError($"Invalid apple-url: '{episodeChangeRequest.Urls.Apple}'.");
                }
            }
        }

        if (episodeChangeRequest.Urls?.YouTube != null)
        {
            if (episodeChangeRequest.Urls.YouTube.ToString() == string.Empty)
            {
                episode.YouTubeId = string.Empty;
                episode.Urls.YouTube = null;
                if (episode.Images != null)
                {
                    episode.Images.YouTube = null;
                }
            }
            else
            {
                if (YouTubePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.YouTube))
                {
                    var youTubeId = YouTubeIdResolver.Extract(episodeChangeRequest.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        episode.YouTubeId = youTubeId;
                        episode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(youTubeId);
                        changeState.UpdateYouTubeImage = true;
                    }
                    else
                    {
                        logger.LogError($"Invalid youtube-url: '{episodeChangeRequest.Urls.YouTube}'.");
                    }
                }
            }
        }

        if (episodeChangeRequest.Urls?.BBC != null)
        {
            if (episodeChangeRequest.Urls.BBC.ToString() == string.Empty)
            {
                episode.Urls.BBC = null;
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesBBC(episodeChangeRequest.Urls.BBC))
                {
                    episode.Urls.BBC = episodeChangeRequest.Urls.BBC;
                    changeState.UpdateBBCImage = true;
                }
            }
        }

        if (episodeChangeRequest.Urls?.InternetArchive != null)
        {
            if (episodeChangeRequest.Urls.InternetArchive.ToString() == string.Empty)
            {
                episode.Urls.InternetArchive = null;
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesInternetArchive(episodeChangeRequest.Urls.InternetArchive))
                {
                    episode.Urls.InternetArchive = episodeChangeRequest.Urls.InternetArchive;
                }
            }
        }

        if (episodeChangeRequest.Images?.Spotify != null)
        {
            episode.Images ??= new EpisodeImages();
            if (episodeChangeRequest.Images.Spotify.ToString() == string.Empty)
            {
                episode.Images.Spotify = null;
            }
            else
            {
                episode.Images.Spotify = episodeChangeRequest.Images.Spotify;
            }
        }

        if (episodeChangeRequest.Images?.Apple != null)
        {
            episode.Images ??= new EpisodeImages();
            if (episodeChangeRequest.Images.Apple.ToString() == string.Empty)
            {
                episode.Images.Apple = null;
            }
            else
            {
                episode.Images.Apple = episodeChangeRequest.Images.Apple;
            }
        }

        if (episodeChangeRequest.Images?.YouTube != null)
        {
            episode.Images ??= new EpisodeImages();
            if (episodeChangeRequest.Images.YouTube.ToString() == string.Empty)
            {
                episode.Images.YouTube = null;
            }
            else
            {
                episode.Images.YouTube = episodeChangeRequest.Images.YouTube;
            }
        }

        if (episodeChangeRequest.Images?.Other != null)
        {
            episode.Images ??= new EpisodeImages();
            if (episodeChangeRequest.Images.Other.ToString() == string.Empty)
            {
                episode.Images.Other = null;
            }
            else
            {
                episode.Images.Other = episodeChangeRequest.Images.Other;
            }
        }
        if (episode.Images != null &&
            episode.Images.YouTube == null &&
            episode.Images.Spotify == null &&
            episode.Images.Apple == null &&
            episode.Images.Other == null)
        {
            episode.Images = null;
        }

        return changeState;
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, Guid episodeId, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation($"{nameof(Get)}: Get episode with id '{episodeId}'.");
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == episodeId));
            var episode = podcast?.Episodes.SingleOrDefault(x => x.Id == episodeId);

            if (episode == null || podcast == null)
            {
                logger.LogWarning($"{nameof(Get)}: Episode with id '{episodeId}' not found.");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var podcastEpisode = episode.Enrich(podcast);
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(podcastEpisode, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to get episode.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c);
        return failure;
    }
}