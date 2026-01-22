using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using PodcastEpisode = RedditPodcastPoster.Models.PodcastEpisode;

namespace Api.Handlers;

public class EpisodeHandler(
    IPodcastRepository podcastRepository,
    SearchClient searchClient,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IContentPublisher contentPublisher,
    IPostManager postManager,
    ITweetManager tweetManager,
    IBlueskyPostManager blueskyPostManager,
    IBlueskyPoster blueskyPoster,
    IShortnerService shortnerService,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ITextSanitiser textSanitiser,
    ICachedSubjectProvider subjectsProvider,
    ILogger<EpisodeHandler> logger) : IEpisodeHandler
{
    private readonly DateTime pastWeek = DateTime.UtcNow.AddDays(-7);

    public async Task<HttpResponseData> Delete(
        HttpRequestData req,
        Guid episodeId,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            var podcasts = await podcastRepository
                .GetAllBy(x => x.Episodes.Any(e => e.Id == episodeId))
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

            await DeleteSearchEntry(podcasts.Single().Name, episodeId, c);

            logger.LogWarning("Delete episode from podcast with id '{podcastId}' and episode-id '{episodeId}'.",
                podcasts.Single().Id, episodeId);
            await podcastRepository.Save(podcasts.Single());
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to delete episode.", nameof(Delete));
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }

    public async Task<HttpResponseData> Publish(
        HttpRequestData req,
        EpisodePublishRequestWrapper publishRequest,
        ClientPrincipal? cp,
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

            var episode = podcast.Episodes.Single(x => x.Id == publishRequest.EpisodeId);

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
                        if (result.TweetSendStatus != TweetSendStatus.Sent)
                        {
                            logger.LogError("Tweet result: '{PostTweetResponse}'.", result);
                            response.FailedTweetContent = result.candidateTweet;
                            response.Tweeted = false;
                        }
                        else
                        {
                            response.Tweeted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        response.Tweeted = false;
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
                            logger.LogError("Bluesky-post result: '{result}'.", result);
                            response.BlueskyPosted = false;
                        }
                        else
                        {
                            response.BlueskyPosted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        response.BlueskyPosted = false;
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
                response.Updated() ? HttpStatusCode.OK : HttpStatusCode.BadRequest
            ).WithJsonBody(response, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish episode.", nameof(Publish));
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }

    public async Task<HttpResponseData> GetOutgoing(
        HttpRequestData req,
        ClientPrincipal? cp,
        CancellationToken c)
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

            var subjects = await subjectsProvider.GetAll().ToListAsync();

            foreach (var podcastId in podcastIds)
            {
                var podcast = await podcastRepository.GetBy(x => x.Id == podcastId.guid);
                var unpostedEpisodes =                    
                    podcast!.Episodes.Where(x =>
                            x.Release > since &&
                            (!x.Posted || posted) &&
                            (!x.Tweeted || tweeted) &&
                            (!(x.BlueskyPosted.HasValue && x.BlueskyPosted.Value) || blueskyPosted))
                        .Select(s =>  s.Enrich(podcast, textSanitiser, subjects));
                episodes.AddRange(unpostedEpisodes);
            }

            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(episodes.OrderByDescending(x => x.Release), c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get out-going episodes.", nameof(GetOutgoing));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c);
        return failure;
    }

    public async Task<HttpResponseData> Post(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{PostName} Episode Change Request: episode-id: '{EpisodeId}'. {Serialize}",
                nameof(Post),
                episodeChangeRequestWrapper.EpisodeId,
                JsonSerializer.Serialize(episodeChangeRequestWrapper.EpisodeChangeRequest));
            var podcast = await podcastRepository.GetBy(x =>
                x.Episodes.Any(ep => ep.Id == episodeChangeRequestWrapper.EpisodeId));
            if (podcast == null)
            {
                logger.LogWarning("Podcast with episode-id '{episodeId}' not found.",
                    episodeChangeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var episode = podcast.Episodes.SingleOrDefault(x => x.Id == episodeChangeRequestWrapper.EpisodeId);
            if (episode == null)
            {
                logger.LogWarning("Episode with id '{episodeId}' not found.", episodeChangeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                "{method} Updating episode-id '{episodeId}' of podcast with id '{podcastId}'. Original-episode: {episode}",
                nameof(Post), episodeChangeRequestWrapper.EpisodeId, podcast.Id, JsonSerializer.Serialize(episode));

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

            await podcastRepository.Save(podcast);

            if (changeState.UnPost)
            {
                await postManager.RemoveEpisodePost(new PodcastEpisode(podcast, episode));
            }
            else if (changeState.UpdatedSubjects)
            {
                await postManager.UpdateFlare(new PodcastEpisode(podcast, episode));
            }

            var removeTweetResult = RemoveTweetState.Unknown;
            if (changeState.UnTweet)
            {
                try
                {
                    removeTweetResult = await tweetManager.RemoveTweet(new PodcastEpisode(podcast, episode));
                    if (removeTweetResult != RemoveTweetState.Deleted)
                    {
                        logger.LogWarning("Failure to delete tweet. Result= {removeTweetResult}.",
                            removeTweetResult);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        "Error using tweet-manager to remove tweet for episode with id '{episodeId}'.",
                        episode.Id);
                    removeTweetResult = RemoveTweetState.Other;
                }
            }

            var removeBlueskyPostResult = RemovePostState.Unknown;
            if (changeState.UnBlueskyPost)
            {
                try
                {
                    removeBlueskyPostResult = await blueskyPostManager.RemovePost(new PodcastEpisode(podcast, episode));
                    if (removeBlueskyPostResult != RemovePostState.Deleted)
                    {
                        logger.LogWarning("Failure to delete bluesky-post. Result= {removeBlueskyPostResult}.",
                            removeBlueskyPostResult);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        "Error using bluesky-post-manager to remove post for episode with id '{episodeId}'.",
                        episode.Id);
                    removeBlueskyPostResult = RemovePostState.Other;
                }
            }

            var respModel = new EpisodeUpdateResponse();
            if (changeState.UnTweet)
            {
                respModel.TweetDeleted = removeTweetResult == RemoveTweetState.Deleted;
            }

            if (changeState.UnBlueskyPost)
            {
                respModel.BlueskyPostDeleted = removeBlueskyPostResult == RemovePostState.Deleted;
            }

            if (episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.HasValue &&
                episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.Value)
            {
                await DeleteSearchEntry(podcast.Name, episodeChangeRequestWrapper.EpisodeId, c);
            }
            else
            {
                var indexed = await episodeSearchIndexerService.IndexEpisode(episodeChangeRequestWrapper.EpisodeId, c);
                respModel.SearchIndexerState = indexed.ToDto();
            }

            if (changeState.PublishHomepage)
            {
                await contentPublisher.PublishHomepage();
            }

            var response = await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(respModel, c);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update episode.", nameof(Post));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c);
        return failure;
    }

    public async Task<HttpResponseData> Get(HttpRequestData req, Guid episodeId, ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{method}: Get episode with id '{episodeId}'.", nameof(Get),
                episodeId);
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == episodeId));
            var episode = podcast?.Episodes.SingleOrDefault(x => x.Id == episodeId);

            if (episode == null || podcast == null)
            {
                logger.LogWarning("{method}: Episode with id '{episodeId}' not found.", nameof(Get), episodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            var subjects = await subjectsProvider.GetAll().ToListAsync();
            var podcastEpisode = episode.Enrich(podcast, textSanitiser, subjects);
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(podcastEpisode, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get episode.", nameof(Get));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c);
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

    private async Task DeleteSearchEntry(
        string podcastName,
        Guid episodeId,
        CancellationToken c)
    {
        try
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                [episodeId.ToString()],
                new IndexDocumentsOptions { ThrowOnAnyError = true },
                c);
            var success = result.Value.Results.First<IndexingResult>().Succeeded;
            if (!success)
            {
                logger.LogError(result.Value.Results.First<IndexingResult>().ErrorMessage);
            }
            else
            {
                logger.LogInformation(
                    "Removed episode from podcast with id '{podcastName}' with episode-id '{episodeId}' from search-index.",
                    podcastName, episodeId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error removing episode from podcast with id '{podcastName}' with episode-id '{episodeId}' from search-index.",
                podcastName, episodeId);
        }
    }

    private EpisodeChangeState UpdateEpisode(Episode episode,
        EpisodeChangeRequest episodeChangeRequest)
    {
        var inPastWeek = episode.Release > pastWeek;
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
            inPastWeek |= episode.Release > pastWeek;
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
                    episode.Images.Spotify = null;
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
                    logger.LogError("Invalid spotify-url: '{spotifyUrl}'.", episodeChangeRequest.Urls.Spotify);
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
                    logger.LogError("Invalid apple-url: '{appleUrl}'.", episodeChangeRequest.Urls.Apple);
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
                        logger.LogError("Invalid youtube-url: '{youTubeUrl}'.", episodeChangeRequest.Urls.YouTube);
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
            episode.Images.Spotify = episodeChangeRequest.Images.Spotify.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Spotify;
        }

        if (episodeChangeRequest.Images?.Apple != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.Apple = episodeChangeRequest.Images.Apple.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Apple;
        }

        if (episodeChangeRequest.Images?.YouTube != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.YouTube = episodeChangeRequest.Images.YouTube.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.YouTube;
        }

        if (episodeChangeRequest.Images?.Other != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.Other = episodeChangeRequest.Images.Other.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Other;
        }

        if (episode.Images != null &&
            episode.Images.YouTube == null &&
            episode.Images.Spotify == null &&
            episode.Images.Apple == null &&
            episode.Images.Other == null)
        {
            episode.Images = null;
        }

        if (episodeChangeRequest.Language != null)
        {
            episode.Language = episodeChangeRequest.Language == string.Empty ? null : episodeChangeRequest.Language;
        }

        if (episodeChangeRequest.HasChange && inPastWeek)
        {
            changeState.PublishHomepage = true;
        }

        if (episodeChangeRequest.TwitterHandles != null)
        {
            episode.TwitterHandles = episodeChangeRequest.TwitterHandles.Length > 0 ? episodeChangeRequest.TwitterHandles : null;
        }

        if (episodeChangeRequest.BlueskyHandles != null)
        {
            episode.BlueskyHandles = episodeChangeRequest.BlueskyHandles.Length > 0 ? episodeChangeRequest.BlueskyHandles : null;
        }

        return changeState;
    }
}