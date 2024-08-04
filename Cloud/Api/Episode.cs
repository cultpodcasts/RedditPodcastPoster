using System.Net;
using System.Text.Json;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace Api;

public class Episode(
    IPodcastRepository podcastRepository,
    ILogger<Episode> logger,
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

    private async Task<HttpResponseData> Post(HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper, CancellationToken c)
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
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                $"{nameof(Post)} Updating episode-id '{episodeChangeRequestWrapper.EpisodeId}' of podcast with id '{podcast.Id}'. Original-episode: {JsonSerializer.Serialize(episode)}");

            UpdateEpisode(episode, episodeChangeRequestWrapper.EpisodeChangeRequest);
            await podcastRepository.Update(podcast);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to get-podcasts.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c);
        return failure;
    }

    private void UpdateEpisode(RedditPodcastPoster.Models.Episode episode, EpisodeChangeRequest episodeChangeRequest)
    {
        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Title))
        {
            episode.Title = episodeChangeRequest.Title;
        }

        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Description))
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

        if (episodeChangeRequest.Urls.Spotify != null &&
            SpotifyPodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Spotify))
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Spotify);
            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                episode.SpotifyId = spotifyId;
                episode.Urls.Spotify = episodeChangeRequest.Urls.Spotify;
            }
        }

        if (episodeChangeRequest.Urls.Apple != null &&
            ApplePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Apple))
        {
            var appleId = AppleIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Apple);
            if (appleId != null)
            {
                episode.AppleId = appleId;
                episode.Urls.Apple = episodeChangeRequest.Urls.Apple;
            }
        }

        if (episodeChangeRequest.Urls.YouTube != null &&
            YouTubePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.YouTube))
        {
            var youTubeId = YouTubeIdResolver.Extract(episodeChangeRequest.Urls.YouTube);
            if (!string.IsNullOrWhiteSpace(youTubeId))
            {
                episode.YouTubeId = youTubeId;
                episode.Urls.YouTube = episodeChangeRequest.Urls.YouTube;
            }
        }
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, Guid episodeId, CancellationToken c)
    {
        try
        {
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == episodeId));
            var episode = podcast?.Episodes.SingleOrDefault(x => x.Id == episodeId);

            if (episode == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(episode, c);
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