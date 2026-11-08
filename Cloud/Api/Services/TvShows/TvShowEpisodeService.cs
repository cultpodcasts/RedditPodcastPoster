using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.TvShows;

public class TvShowEpisodeService(
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    CataloguePlayableSearchSync searchSync,
    IPlayableSocialRetraction socialRetraction,
    ILogger<TvShowEpisodeService> logger) : ITvShowEpisodeService
{
    public async Task<TvShowEpisodeGetResult> GetAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == episodeId);
            return episode is null
                ? new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.NotFound)
                : new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.Found, episode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get TV-show episode '{id}'.", nameof(GetAsync), episodeId);
            return new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.Failed);
        }
    }

    public async Task<TvShowEpisodeUpdateResult> UpdateAsync(
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == request.EpisodeId);
            if (episode is null)
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.NotFound);
            }

            // Projected before the PATCH, so the match uses the release, length and AT URI the post was made with.
            var target = SocialPostRemovalTarget.For(episode);

            if (!PlayableChangeApplier.TryApply(episode, request.Change, out var postStatus, out var playableError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, playableError);
            }

            if (!CanonicalIdChangeApplier.TryValidate(
                    request.Change.ImdbId,
                    request.Change.TmdbId,
                    request.Change.TvdbId,
                    out var idError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, idError);
            }

            CanonicalIdChangeApplier.Apply(
                episode,
                request.Change.ImdbId,
                request.Change.TmdbId,
                request.Change.TvdbId);

            // Both deletes run before the save, so the stored flags only drop once a delete has succeeded:
            // Bluesky needs the AT URI still stored; tweeted is cleared only when the tweet was deleted.
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, episode, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, episode, target)
            };
            await tvShowEpisodeRepository.Save(episode);

            // As news reports: removed=true always deletes the search document and the short link (the sibling pull
            // indexer never deletes); any other edit pushes the document now.
            outcome.SearchIndexer = await searchSync.AfterSave(
                episode, target.Projection, request.Change.Removed == true, cancellationToken);

            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update TV-show episode '{id}'.", nameof(UpdateAsync),
                request.EpisodeId);
            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Failed);
        }
    }

    public async Task<EpisodeDeleteResult> DeleteAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == episodeId);
            if (episode is null)
            {
                logger.LogWarning("Delete: missing TV-show episode '{episodeId}'.", episodeId);
                return new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound);
            }

            if (episode.Tweeted)
            {
                return new EpisodeDeleteResult(
                    EpisodeDeleteStatus.AlreadySocial,
                    Tweeted: true);
            }

            var shortLinkTarget = SocialPostRemovalTarget.For(episode).Projection;
            await tvShowEpisodeRepository.Delete(episode.TvShowId, episode.Id);
            await searchSync.AfterHardDelete(
                episode.Id,
                episode.TvShowName ?? string.Empty,
                shortLinkTarget,
                cancellationToken);

            logger.LogWarning(
                "Delete removed TV-show episode '{episodeId}' from show '{tvShowId}'.",
                episode.Id, episode.TvShowId);
            return new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete TV-show episode '{episodeId}'.", episodeId);
            return new EpisodeDeleteResult(EpisodeDeleteStatus.Failed);
        }
    }
}
