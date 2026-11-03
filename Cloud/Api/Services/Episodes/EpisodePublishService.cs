using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

public class EpisodePublishService(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    ICataloguePlayableResolver cataloguePlayableResolver,
    ICataloguePlayableSocialPosts cataloguePlayableSocialPosts,
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
            var resolved = await ResolvePlayable(publishRequest, cancellationToken);
            if (resolved == null)
            {
                throw new ArgumentException($"Episode with id '{publishRequest.EpisodeId}' not found.");
            }

            var podcastEpisode = resolved.Value.PodcastEpisode;
            var outcome = new EpisodePublishOutcome(resolved.Value.OutcomePodcastId);

            if (publishRequest.EpisodePublishRequest.Post)
            {
                // Live Reddit.NET posting removed; keep API `post` flag for a future Devvit poster.
                logger.LogInformation(
                    "Reddit posting is retired; skipping post for episode '{EpisodeId}'.",
                    podcastEpisode.Episode.Id);
                outcome.Posted = podcastEpisode.Episode.Posted;
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
                        var result = await tweetPoster.PostTweet(podcastEpisode, shortnerResult.Url, shortnerResult.HasShareImage);
                        if (result.TweetSendStatus == TweetSendStatus.PaymentRequired)
                        {
                            logger.LogError(
                                "X credits depleted (402 Payment Required); tweet not sent for episode-id '{EpisodeId}'.",
                                podcastEpisode.Episode.Id);
                        }

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
                            podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                    }
                }

                if (publishRequest.EpisodePublishRequest.BlueskyPost)
                {
                    try
                    {
                        var result = await blueskyPoster.Post(podcastEpisode, shortnerResult.Url, shortnerResult.HasShareImage);
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
                            podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                    }
                }
            }

            if (outcome.Updated())
            {
                var needsUnblock = podcastEpisode.Episode.Ignored || podcastEpisode.Episode.IsRemoved();
                if (podcastEpisode.IsCataloguePlayable)
                {
                    // Posters already persisted tweet/Bluesky state on the catalogue document. Never
                    // IEpisodeRepository.Save a projection — that would write the wrong container.
                    if (needsUnblock)
                    {
                        await cataloguePlayableSocialPosts.ClearIgnoredAndRemoved(podcastEpisode);
                    }
                }
                else
                {
                    if (podcastEpisode.Episode.Ignored)
                    {
                        podcastEpisode.Episode.Ignored = false;
                    }

                    if (podcastEpisode.Episode.IsRemoved())
                    {
                        podcastEpisode.Episode.Removed = false;
                    }

                    await episodeRepository.Save(podcastEpisode.Episode);
                }
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

    /// <summary>
    /// Podcast episode first; on miss, the same catalogue fallback as GET /episode/{id}.
    /// </summary>
    private async Task<(PodcastEpisode PodcastEpisode, Guid OutcomePodcastId)?> ResolvePlayable(
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken cancellationToken)
    {
        var podcastEpisodeResolverResponse =
            await podcastEpisodeResolver.ResolvePodcast(publishRequest.ToPodcastEpisodeResolverRequest(),
                nameof(PublishAsync));

        if (podcastEpisodeResolverResponse.Episode != null)
        {
            if (podcastEpisodeResolverResponse.Podcast == null ||
                podcastEpisodeResolverResponse.Podcast.Removed == true)
            {
                throw new ArgumentException(
                    $"Podcast for episode-id '{publishRequest.EpisodeId}' not found or removed.");
            }

            return (
                new PodcastEpisode(podcastEpisodeResolverResponse.Podcast, podcastEpisodeResolverResponse.Episode),
                podcastEpisodeResolverResponse.Podcast.Id);
        }

        var catalogue = await cataloguePlayableResolver.Resolve(publishRequest.EpisodeId, cancellationToken);
        if (catalogue == null)
        {
            return null;
        }

        if (catalogue.Removed)
        {
            throw new ArgumentException(
                $"Catalogue playable '{publishRequest.EpisodeId}' ({catalogue.ContentKind}) is removed.");
        }

        // Film has no parent: outcome id is the film id (projection podcast stand-in). News/TV use the parent.
        var outcomePodcastId = catalogue.ParentId ?? catalogue.Projection.Podcast.Id;
        return (catalogue.Projection, outcomePodcastId);
    }
}
