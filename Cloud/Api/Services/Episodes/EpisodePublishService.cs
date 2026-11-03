using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Extensions;
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
            var resolveOutcome = await ResolvePlayable(publishRequest, cancellationToken);
            if (resolveOutcome.ClientFailure is { } clientFailure)
            {
                return new EpisodePublishResult(clientFailure);
            }

            var podcastEpisode = resolveOutcome.Value!.Value.PodcastEpisode;
            var outcome = new EpisodePublishOutcome(resolveOutcome.Value.Value.OutcomePodcastId);

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
                // Catalogue kinds use /film|/news|/tv routes — never the podcast-shaped shortener KV.
                Uri? shortUrl;
                var hasShareImage = false;
                if (podcastEpisode.IsCataloguePlayable)
                {
                    shortUrl = CataloguePlayableSiteLink.For(podcastEpisode);
                }
                else
                {
                    var shortnerResult = await shortnerService.Write(podcastEpisode);
                    if (!shortnerResult.Success)
                    {
                        logger.LogError("Unsuccessful shortening-url.");
                    }

                    shortUrl = shortnerResult.Url;
                    hasShareImage = shortnerResult.HasShareImage;
                }

                if (publishRequest.EpisodePublishRequest.Tweet)
                {
                    try
                    {
                        var result = await tweetPoster.PostTweet(podcastEpisode, shortUrl, hasShareImage);
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
                        var result = await blueskyPoster.Post(podcastEpisode, shortUrl, hasShareImage);
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
    /// Kind publish routes resolve catalogue only (single-container when <see cref="EpisodePublishRequestWrapper.ExpectedKind"/>
    /// is set); guid-only is podcast episode first then three-way catalogue (same fallback as GET /episode/{id}).
    /// Client resolve failures return <see cref="EpisodePublishStatus.NotFound"/> — not throw → Failed/500.
    /// </summary>
    private async Task<PlayableResolveOutcome> ResolvePlayable(
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken cancellationToken)
    {
        if (publishRequest.ExpectedKind is ContentKind.Film
            or ContentKind.NewsReport
            or ContentKind.TvShowEpisode)
        {
            return await ResolveCataloguePlayable(
                publishRequest.EpisodeId,
                publishRequest.ExpectedKind.Value,
                cancellationToken);
        }

        var podcastEpisodeResolverResponse =
            await podcastEpisodeResolver.ResolvePodcast(publishRequest.ToPodcastEpisodeResolverRequest(),
                nameof(PublishAsync));

        if (podcastEpisodeResolverResponse.Episode != null)
        {
            if (podcastEpisodeResolverResponse.Podcast == null ||
                podcastEpisodeResolverResponse.Podcast.Removed == true)
            {
                return PlayableResolveOutcome.NotFound();
            }

            return PlayableResolveOutcome.Ok(
                new PodcastEpisode(podcastEpisodeResolverResponse.Podcast, podcastEpisodeResolverResponse.Episode),
                podcastEpisodeResolverResponse.Podcast.Id);
        }

        return await ResolveCataloguePlayable(publishRequest.EpisodeId, expectedKind: null, cancellationToken);
    }

    private async Task<PlayableResolveOutcome> ResolveCataloguePlayable(
        Guid playableId,
        ContentKind? expectedKind,
        CancellationToken cancellationToken)
    {
        var catalogue = expectedKind is { } kind
            ? await cataloguePlayableResolver.Resolve(playableId, kind, cancellationToken)
            : await cataloguePlayableResolver.Resolve(playableId, cancellationToken);
        if (catalogue == null)
        {
            return PlayableResolveOutcome.NotFound();
        }

        // Defensive: kind-scoped resolve should only return the expected container; still reject mismatch as 404.
        if (expectedKind is { } expected &&
            !string.Equals(catalogue.ContentKind, expected.ToString(), StringComparison.Ordinal))
        {
            return PlayableResolveOutcome.NotFound();
        }

        // Item-level Removed may still publish (then ClearIgnoredAndRemoved). Parent-removed blocks like a removed podcast.
        if (catalogue.ParentRemoved)
        {
            return PlayableResolveOutcome.NotFound();
        }

        // Film has no parent: outcome id is the film id (projection podcast stand-in). News/TV use the parent.
        var outcomePodcastId = catalogue.ParentId ?? catalogue.Projection.Podcast.Id;
        return PlayableResolveOutcome.Ok(catalogue.Projection, outcomePodcastId);
    }

    private readonly record struct PlayableResolveOutcome(
        EpisodePublishStatus? ClientFailure,
        (PodcastEpisode PodcastEpisode, Guid OutcomePodcastId)? Value)
    {
        public static PlayableResolveOutcome Ok(PodcastEpisode podcastEpisode, Guid outcomePodcastId) =>
            new(null, (podcastEpisode, outcomePodcastId));

        public static PlayableResolveOutcome NotFound() =>
            new(EpisodePublishStatus.NotFound, null);
    }
}
