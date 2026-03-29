using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
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
using Episode = RedditPodcastPoster.Models.Episode;
using Podcast = RedditPodcastPoster.Models.Podcast;
using PodcastEpisode = RedditPodcastPoster.Models.PodcastEpisode;
using Subject = RedditPodcastPoster.Models.Subject;

namespace Api.Handlers;

public class EpisodeHandler(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    SearchClient searchClient,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IHomepagePublisher contentPublisher,
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
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    podcastEpisodeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(Delete));

            if (podcastEpisodeResolverResponse.State == PodcastEpisodeResolveState.PodcastConflict)
            {
                logger.LogWarning(
                    "{method} Multiple podcasts with Podcast-Name: '{podcastName}', Episode-id: '{episodeId}'.",
                    nameof(Delete), podcastEpisodeRequestWrapper.PodcastName, podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.Conflict);
            }
            else if (podcastEpisodeResolverResponse.Episode == null || podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning(
                    "{method} missing episode or podcast. Podcast-Name: '{podcastName}', Episode-id: '{episodeId}'.",
                    nameof(Delete), podcastEpisodeRequestWrapper.PodcastName, podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (podcastEpisodeResolverResponse.Episode.Tweeted || podcastEpisodeResolverResponse.Episode.Posted)
            {
                return await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(
                    new
                    {
                        message = "Cannot remove episode.", posted = podcastEpisodeResolverResponse.Episode.Posted,
                        tweeted = podcastEpisodeResolverResponse.Episode.Tweeted
                    }, c);
            }

            await episodeRepository.Delete(podcastEpisodeResolverResponse.Episode.PodcastId,
                podcastEpisodeResolverResponse.Episode.Id);

            await DeleteSearchEntry(podcastEpisodeResolverResponse.Podcast.Name, podcastEpisodeRequestWrapper.EpisodeId,
                c);
            await shortnerService.Delete(new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                podcastEpisodeResolverResponse.Episode));

            logger.LogWarning(
                "Delete detached episode from podcast with id '{podcastId}' and episode-id '{episodeId}'.",
                podcastEpisodeResolverResponse.Podcast.Id, podcastEpisodeRequestWrapper.EpisodeId);
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
            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(publishRequest.ToPodcastEpisodeResolverRequest(),
                    nameof(Publish));

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                throw new ArgumentException($"Episode with id '{publishRequest.EpisodeId}' not found.");
            }

            if (podcastEpisodeResolverResponse.Podcast == null ||
                podcastEpisodeResolverResponse.Podcast.Removed == true)
            {
                throw new ArgumentException(
                    $"Podcast for episode-id '{publishRequest.EpisodeId}' not found or removed.");
            }

            var podcastEpisode = new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                podcastEpisodeResolverResponse.Episode);

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
                            podcastEpisodeResolverResponse.Podcast.Id, podcastEpisodeResolverResponse.Episode.Id);
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
                            podcastEpisodeResolverResponse.Podcast.Id, podcastEpisodeResolverResponse.Episode.Id);
                    }
                }
            }

            if (response.Updated())
            {
                if (podcastEpisodeResolverResponse.Episode.Ignored)
                {
                    podcastEpisodeResolverResponse.Episode.Ignored = false;
                }

                if (podcastEpisodeResolverResponse.Episode.Removed)
                {
                    podcastEpisodeResolverResponse.Episode.Removed = false;
                }

                await episodeRepository.Save(podcastEpisodeResolverResponse.Episode);
            }

            var httpStatusCode = response.Updated() ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            var success = await req.CreateResponse(httpStatusCode).WithJsonBody(response, c);
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
            var subjects = await subjectsProvider.GetAll().ToListAsync(c);

            // Use cached episodes from provider instead of cross-partition query
            var podcastEpisodes = await recentEpisodeCandidatesProvider.GetEpisodes(since);

            foreach (var podcastEpisode in podcastEpisodes)
            {
                var episode = podcastEpisode.Episode;
                var podcast = podcastEpisode.Podcast;

                // Skip removed
                if (episode.Removed)
                {
                    continue;
                }

                // Skip posted episodes if not explicitly requested
                if (episode.Posted && !posted)
                {
                    continue;
                }

                // Skip tweeted episodes if not explicitly requested
                if (episode.Tweeted && !tweeted)
                {
                    continue;
                }

                // Skip bluesky-posted episodes if not explicitly requested
                if (episode.BlueskyPosted.HasValue && episode.BlueskyPosted.Value && !blueskyPosted)
                {
                    continue;
                }

                episodes.Add(await ToDiscreteEpisode(episode, podcast, subjects));
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

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    episodeChangeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(Post));


            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("Episode with id '{episodeId}' not found.", episodeChangeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("Podcast with id '{podcastId}' not found for episode-id '{episodeId}'.",
                    podcastEpisodeResolverResponse.Episode.PodcastId, episodeChangeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                "{method} Updating episode-id '{episodeId}' of podcast with id '{podcastId}'. Original-episode: {episode}",
                nameof(Post), episodeChangeRequestWrapper.EpisodeId, podcastEpisodeResolverResponse.Podcast.Id,
                JsonSerializer.Serialize(podcastEpisodeResolverResponse.Episode));

            var changeState = UpdateEpisode(podcastEpisodeResolverResponse.Episode,
                episodeChangeRequestWrapper.EpisodeChangeRequest);

            var indexingContext = new IndexingContext();
            if (changeState.UpdateImages)
            {
                await imageUpdater.UpdateImages(
                    podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode,
                    new EpisodeImageUpdateRequest(
                        changeState.UpdateSpotifyImage,
                        changeState.UpdateAppleImage,
                        changeState.UpdateYouTubeImage,
                        changeState.UpdateBBCImage),
                    indexingContext);
            }

            await episodeRepository.Save(podcastEpisodeResolverResponse.Episode);

            var podcastEpisode = new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                podcastEpisodeResolverResponse.Episode);

            if (changeState.UnPost)
            {
                await postManager.RemoveEpisodePost(podcastEpisode);
            }
            else if (changeState.UpdatedSubjects)
            {
                await postManager.UpdateFlare(podcastEpisode);
            }

            var removeTweetResult = RemoveTweetState.Unknown;
            if (changeState.UnTweet)
            {
                try
                {
                    removeTweetResult = await tweetManager.RemoveTweet(podcastEpisode);
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
                        podcastEpisodeResolverResponse.Episode.Id);
                    removeTweetResult = RemoveTweetState.Other;
                }
            }

            var removeBlueskyPostResult = RemovePostState.Unknown;
            if (changeState.UnBlueskyPost)
            {
                try
                {
                    removeBlueskyPostResult = await blueskyPostManager.RemovePost(podcastEpisode);
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
                        podcastEpisodeResolverResponse.Episode.Id);
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
                await DeleteSearchEntry(podcastEpisodeResolverResponse.Podcast.Name,
                    episodeChangeRequestWrapper.EpisodeId, c);
                await shortnerService.Delete(new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode));
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

    public async Task<HttpResponseData> Get(HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper, ClientPrincipal? cp,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{method}: Get episode with id '{episodeId}'.", nameof(Get),
                podcastEpisodeRequestWrapper.EpisodeId);

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    podcastEpisodeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(Get));

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("{method}: Episode with name '{episodeId}' not found.", nameof(Get),
                    podcastEpisodeRequestWrapper.EpisodeId);
                return await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { message = "Episode not found." }, c);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("{method}: Podcast with id '{podcastName}' not found for episode-id '{episodeId}'.",
                    nameof(Get), podcastEpisodeResolverResponse,
                    podcastEpisodeRequestWrapper.PodcastName);
                return await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { message = "Podcast not found." }, c);
            }

            var subjects = await subjectsProvider.GetAll().ToListAsync(c);
            var discreteEpisode = await ToDiscreteEpisode(podcastEpisodeResolverResponse.Episode,
                podcastEpisodeResolverResponse.Podcast, subjects);
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(discreteEpisode, c);
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

    private async Task<DiscreteEpisode> ToDiscreteEpisode(Episode episode, Podcast podcast,
        IEnumerable<Subject> subjects)
    {
        var episodeSubjects = subjects
            .Where(s => episode.Subjects.Contains(s.Name))
            .SelectMany(s => s.KnownTerms ?? Array.Empty<string>())
            .ToArray();

        var titleRegex = string.IsNullOrWhiteSpace(podcast.TitleRegex)
            ? null
            : new Regex(podcast.TitleRegex, RegexOptions.IgnoreCase);

        var descriptionRegex = string.IsNullOrWhiteSpace(podcast.DescriptionRegex)
            ? null
            : new Regex(podcast.DescriptionRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return new DiscreteEpisode
        {
            Id = episode.Id,
            PodcastName = podcast.Name,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Release = episode.Release,
            Removed = episode.Removed,
            Length = episode.Length,
            Explicit = episode.Explicit,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Images = episode.Images,
            Subjects = episode.Subjects,
            SearchTerms = episode.SearchTerms,
            YouTubePodcast = !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId),
            SpotifyPodcast = !string.IsNullOrWhiteSpace(podcast.SpotifyId),
            ApplePodcast = podcast.AppleId != null,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            Image =
                episode.Images?.YouTube ?? episode.Images?.Spotify ?? episode.Images?.Apple ?? episode.Images?.Other,
            Language = episode.Language,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles,
            DisplayTitle = await textSanitiser.SanitiseTitle(
                episode.Title,
                titleRegex,
                podcast.KnownTerms ?? Array.Empty<string>(),
                episodeSubjects),
            DisplayDescription = textSanitiser.SanitiseDescription(episode.Description, descriptionRegex)
        };
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
            episode.Language =
                episodeChangeRequest.Language == string.Empty ? null : episodeChangeRequest.Language;
        }

        if (episodeChangeRequest.HasChange && inPastWeek)
        {
            changeState.PublishHomepage = true;
        }

        if (episodeChangeRequest.TwitterHandles != null)
        {
            episode.TwitterHandles = episodeChangeRequest.TwitterHandles.Length > 0
                ? episodeChangeRequest.TwitterHandles
                : null;
        }

        if (episodeChangeRequest.BlueskyHandles != null)
        {
            episode.BlueskyHandles = episodeChangeRequest.BlueskyHandles.Length > 0
                ? episodeChangeRequest.BlueskyHandles
                : null;
        }

        return changeState;
    }
}