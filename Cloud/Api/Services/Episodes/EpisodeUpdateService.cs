using System.Text.Json;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Reddit.Managers;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

public class EpisodeUpdateService(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    EpisodeChangeApplier episodeChangeApplier,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IHomepagePublisher contentPublisher,
    IPostManager postManager,
    ITweetManager tweetManager,
    IBlueskyPostManager blueskyPostManager,
    IShortnerService shortnerService,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<EpisodeUpdateService> logger) : IEpisodeUpdateService
{
    public async Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{PostName} Episode Change Request: episode-id: '{EpisodeId}'. {Serialize}",
                nameof(UpdateAsync),
                episodeChangeRequestWrapper.EpisodeId,
                JsonSerializer.Serialize(episodeChangeRequestWrapper.EpisodeChangeRequest));

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    episodeChangeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(UpdateAsync));

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("Episode with id '{episodeId}' not found.", episodeChangeRequestWrapper.EpisodeId);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("Podcast with id '{podcastId}' not found for episode-id '{episodeId}'.",
                    podcastEpisodeResolverResponse.Episode.PodcastId, episodeChangeRequestWrapper.EpisodeId);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            logger.LogInformation(
                "{method} Updating episode-id '{episodeId}' of podcast with id '{podcastId}'. Original-episode: {episode}",
                nameof(UpdateAsync), episodeChangeRequestWrapper.EpisodeId, podcastEpisodeResolverResponse.Podcast.Id,
                JsonSerializer.Serialize(podcastEpisodeResolverResponse.Episode));

            var changeState = episodeChangeApplier.Apply(
                podcastEpisodeResolverResponse.Episode,
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

            var outcome = new EpisodeUpdateOutcome();
            if (changeState.UnTweet)
            {
                outcome.TweetDeleted = removeTweetResult == RemoveTweetState.Deleted;
            }

            if (changeState.UnBlueskyPost)
            {
                outcome.BlueskyPostDeleted = removeBlueskyPostResult == RemovePostState.Deleted;
            }

            if (episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.HasValue &&
                episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.Value)
            {
                await searchIndexCleanup.DeleteSearchEntry(
                    podcastEpisodeResolverResponse.Podcast.Name,
                    episodeChangeRequestWrapper.EpisodeId,
                    cancellationToken);
                await shortnerService.Delete(new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode));

                if (changeState.PublishHomepage)
                {
                    await contentPublisher.PublishHomepage();
                }
            }
            else
            {
                var indexTask = episodeSearchIndexerService.IndexEpisode(
                    podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode,
                    cancellationToken);
                var homepageTask = changeState.PublishHomepage
                    ? contentPublisher.PublishHomepage()
                    : Task.CompletedTask;

                await Task.WhenAll(indexTask, homepageTask);

                outcome.SearchIndexer = await indexTask;
            }

            return new EpisodeUpdateResult(EpisodeUpdateStatus.Accepted, outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update episode.", nameof(UpdateAsync));
            return new EpisodeUpdateResult(EpisodeUpdateStatus.Failed);
        }
    }
}
