﻿using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Extensions;
using Azure.Search.Documents;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Twitter;
using Podcast = RedditPodcastPoster.Models.Podcast;
using PodcastEpisode = RedditPodcastPoster.Models.PodcastEpisode;

namespace Api;

public class EpisodeController(
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IContentPublisher contentPublisher,
    IPostManager postManager,
    ILogger<EpisodeController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
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

    private async Task<HttpResponseData> Publish(HttpRequestData req, EpisodePublishRequestWrapper publishRequest,
        CancellationToken c)
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

            var podcastEpisode = new PodcastEpisode(podcast, episode);

            if (publishRequest.EpisodePublishRequest.Post)
            {
                var result = await podcastEpisodePoster.PostPodcastEpisode(podcastEpisode);
                if (!result.Success)
                {
                    logger.LogError(result.ToString());
                }

                response.Posted = result.Success;
            }

            if (publishRequest.EpisodePublishRequest.Tweet)
            {
                var result = await tweetPoster.PostTweet(podcastEpisode);
                if (result != TweetSendStatus.Sent)
                {
                    logger.LogError($"Tweet result: '{result}'.");
                }
                else
                {
                    response.Tweeted = true;
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

    private async Task<HttpResponseData> GetOutgoing(HttpRequestData req, CancellationToken c)
    {
        try
        {
            var (days, posted, tweeted) = ParseOutgoingQuery(req);
            if (posted && tweeted)
            {
                var invalidArguments = await req.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(
                        SubmitUrlResponse.Failure($"Invalid arguments. Posted='{posted}', Tweeted='{tweeted}'."), c);
                return invalidArguments;
            }

            var episodes = new List<DiscreteEpisode>();
            var since = DateTimeExtensions.DaysAgo(days);
            var podcastIds = await podcastRepository.GetAllBy(x =>
                    (
                        !x.Removed.IsDefined() ||
                        x.Removed == false
                    ) &&
                    x.Episodes.Any(ep => ep.Release > since && (!ep.Posted || posted) && (!ep.Tweeted || tweeted)),
                x => new {guid = x.Id}).ToListAsync(c);
            foreach (var podcastId in podcastIds)
            {
                var podcast = await podcastRepository.GetBy(x => x.Id == podcastId.guid);
                var unpostedEpisodes =
                    podcast.Episodes.Where(x => x.Release > since && (!x.Posted || posted) && (!x.Tweeted || tweeted))
                        .Select(x => x.Enrich(podcast.Name));
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

    private (int days, bool posted, bool tweeted) ParseOutgoingQuery(HttpRequestData req)
    {
        if (!bool.TryParse(req.Query["tweeted"], out var tweeted))
        {
            tweeted = false;
        }

        if (!bool.TryParse(req.Query["posted"], out var posted))
        {
            posted = false;
        }

        if (!int.TryParse(req.Query["days"], out var days))
        {
            days = 7;
        }

        if (days > 14)
        {
            days = 14;
        }

        return (days, posted, tweeted);
    }

    private async Task<HttpResponseData> Post(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
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
            await podcastRepository.Update(podcast);

            if (episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.HasValue &&
                episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.Value)
            {
                await DeleteSearchEntry(episodeChangeRequestWrapper, podcast, c);
            }

            if (changeState.UnPost)
            {
                await postManager.RemoveEpisodePost(new PodcastEpisode(podcast, episode));
            }

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Post)}: Failed to update episode.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c);
        return failure;
    }

    private async Task DeleteSearchEntry(EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        Podcast podcast,
        CancellationToken c)
    {
        try
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                new[] {episodeChangeRequestWrapper.EpisodeId.ToString()},
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
                    $"Removed episode from podcast with id '{podcast.Id}' with episode-id '{episodeChangeRequestWrapper.EpisodeId}' from search-index.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Error removing episode from podcast with id '{podcast.Id}' with episode-id '{episodeChangeRequestWrapper.EpisodeId}' from search-index.");
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
            episode.Tweeted = episodeChangeRequest.Tweeted.Value;
        }

        if (episodeChangeRequest.Subjects != null && !episode.Subjects.SequenceEqual(episodeChangeRequest.Subjects))
        {
            episode.Subjects = episodeChangeRequest.Subjects.ToList();
        }

        if (episodeChangeRequest.Urls.Spotify != null)
        {
            if (episodeChangeRequest.Urls.Spotify.ToString() == string.Empty)
            {
                episode.SpotifyId = string.Empty;
                episode.Urls.Spotify = null;
            }
            else
            {
                if (SpotifyPodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Spotify))
                {
                    var spotifyId = SpotifyIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Spotify);
                    if (!string.IsNullOrWhiteSpace(spotifyId))
                    {
                        episode.SpotifyId = spotifyId;
                        episode.Urls.Spotify = episodeChangeRequest.Urls.Spotify;
                    }
                }
                else
                {
                    logger.LogError($"Invalid spotify-url: '{episodeChangeRequest.Urls.Spotify}'.");
                }
            }
        }

        if (episodeChangeRequest.Urls.Apple != null)
        {
            if (episodeChangeRequest.Urls.Apple.ToString() == string.Empty)
            {
                episode.AppleId = null;
                episode.Urls.Apple = null;
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
                    }
                }
                else
                {
                    logger.LogError($"Invalid apple-url: '{episodeChangeRequest.Urls.Apple}'.");
                }
            }
        }

        if (episodeChangeRequest.Urls.YouTube != null)
        {
            if (episodeChangeRequest.Urls.YouTube.ToString() == string.Empty)
            {
                episode.YouTubeId = string.Empty;
                episode.Urls.YouTube = null;
            }
            else
            {
                if (YouTubePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.YouTube))
                {
                    {
                        var youTubeId = YouTubeIdResolver.Extract(episodeChangeRequest.Urls.YouTube);
                        if (!string.IsNullOrWhiteSpace(youTubeId))
                        {
                            episode.YouTubeId = youTubeId;
                            episode.Urls.YouTube = episodeChangeRequest.Urls.YouTube;
                        }
                        else
                        {
                            logger.LogError($"Invalid youtube-url: '{episodeChangeRequest.Urls.YouTube}'.");
                        }
                    }
                }
            }
        }

        return changeState;
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, Guid episodeId, CancellationToken c)
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

            var podcastEpisode = episode.Enrich(podcast.Name);
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