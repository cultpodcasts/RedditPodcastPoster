using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

public class EpisodePublishService(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IBlueskyPoster blueskyPoster,
    IShortnerService shortnerService,
    ILogger<EpisodePublishService> logger) : IEpisodePublishService
{
    public async Task<EpisodePublishResult> PublishAsync(
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            EpisodePublishOutcome outcome;
            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(publishRequest.ToPodcastEpisodeResolverRequest(),
                    nameof(PublishAsync));

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
            else
            {
                outcome = new EpisodePublishOutcome(podcastEpisodeResolverResponse.Podcast.Id);
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

                outcome.Posted = result.Success;
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
                            outcome.FailedTweetContent = result.candidateTweet;
                            outcome.Tweeted = false;
                        }
                        else
                        {
                            outcome.Tweeted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        outcome.Tweeted = false;
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
                            outcome.BlueskyPosted = false;
                        }
                        else
                        {
                            outcome.BlueskyPosted = true;
                        }
                    }
                    catch (Exception e)
                    {
                        outcome.BlueskyPosted = false;
                        logger.LogError(e,
                            "Failed to bluesky-post for podcast-id: {podcastId} and episode-id {episodeId}.",
                            podcastEpisodeResolverResponse.Podcast.Id, podcastEpisodeResolverResponse.Episode.Id);
                    }
                }
            }

            if (outcome.Updated())
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

            return new EpisodePublishResult(
                outcome.Updated() ? EpisodePublishStatus.Ok : EpisodePublishStatus.BadRequest,
                outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish episode.", nameof(PublishAsync));
            return new EpisodePublishResult(EpisodePublishStatus.Failed);
        }
    }
}
