using Api.Models;
using Api.Services;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.TvShows;

public class TvShowEpisodeService(
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ITvShowRepository tvShowRepository,
    CataloguePlayableSearchSync searchSync,
    IPlayableSocialRetraction socialRetraction,
    IIdentityMetadataEnricher identityMetadata,
    IUrlMembershipLookup urlMembershipLookup,
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
            var hadAdditionalYouTube = episode.AdditionalYouTube is { Count: > 0 };

            if (!CanonicalIdChangeApplier.TryValidate(
                    request.Change.ImdbId,
                    request.Change.TmdbId,
                    request.Change.TvdbId,
                    out var idError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, idError);
            }

            if (request.Change.AdditionalYouTube is not null)
            {
                if (!PlayableChangeApplier.TryValidate(episode, request.Change, out var validateError))
                {
                    return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, validateError);
                }

                var held = await YouTubeConflict(episode, request.Change, cancellationToken);
                if (held is not null)
                {
                    return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Conflict, held);
                }

                var saved = await SaveAdditionalYouTube(episode, request.Change, target, cancellationToken);
                if (saved.Result.Status != TvShowEpisodeUpdateStatus.Accepted || saved.Episode is null)
                {
                    return saved.Result;
                }

                var cleared = hadAdditionalYouTube && saved.Episode.AdditionalYouTube is not { Count: > 0 };
                saved.Result.Outcome!.SearchIndexer = await searchSync.AfterSave(
                    saved.Episode,
                    target.Projection,
                    request.Change.Removed == true,
                    cancellationToken,
                    youTubeAlternatesCleared: cleared);
                return saved.Result;
            }

            if (!PlayableChangeApplier.TryApply(episode, request.Change, out var postStatus, out var playableError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, playableError);
            }

            var readIds = (episode.ImdbId, episode.TvdbId, episode.TmdbId);
            CanonicalIdChangeApplier.Apply(
                episode,
                request.Change.ImdbId,
                request.Change.TmdbId,
                request.Change.TvdbId);

            await FillFromIdentity(
                episode,
                readIds != (episode.ImdbId, episode.TvdbId, episode.TmdbId),
                SkipFrom(request.Change),
                cancellationToken);

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
            var youTubeAlternatesCleared = hadAdditionalYouTube && episode.AdditionalYouTube is not { Count: > 0 };
            outcome.SearchIndexer = await searchSync.AfterSave(
                episode,
                target.Projection,
                request.Change.Removed == true,
                cancellationToken,
                youTubeAlternatesCleared: youTubeAlternatesCleared);

            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update TV-show episode '{id}'.", nameof(UpdateAsync),
                request.EpisodeId);
            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Failed);
        }
    }

    /// <summary>
    /// RPP #1430: when the episode has an IMDb or TheTVDB id and either an id changed in this edit or a field identity
    /// can fill is empty (name, overview, air date, runtime, still), fills only those empty fields from TMDB.
    /// A failed lookup leaves the edit as the curator sent it.
    /// </summary>
    private async Task FillFromIdentity(
        TvShowEpisode episode,
        bool idsChanged,
        IdentityFieldSkip skip,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(episode.ImdbId) && episode.TvdbId is not > 0)
        {
            return;
        }

        if (!idsChanged && !IdentityMetadataFill.HasGaps(episode, skip))
        {
            return;
        }

        var show = await tvShowRepository.GetTvShow(episode.TvShowId);
        var metadata = await identityMetadata.FetchTvShowEpisodeAsync(episode, show?.TmdbId, cancellationToken);
        if (metadata is not null)
        {
            IdentityMetadataFill.Fill(episode, metadata, skip);
        }
    }

    private async Task<string?> YouTubeConflict(
        TvShowEpisode episode,
        TvShowEpisodeChangeRequest change,
        CancellationToken cancellationToken) =>
        await YouTubeAlternateOwnership.ConflictMessageIfHeld(
            urlMembershipLookup,
            YouTubeAlternateChangeApplier.NewIds(
                episode,
                change.AdditionalYouTube,
                YouTubeAlternateChangeApplier.ResultingPrimaryYouTubeId(episode, change.Services)),
            [episode.Id],
            cancellationToken);

    private readonly record struct TvShowEpisodePersist(TvShowEpisodeUpdateResult Result, TvShowEpisode? Episode);

    private async Task<TvShowEpisodePersist> SaveAdditionalYouTube(
        TvShowEpisode firstRead,
        TvShowEpisodeChangeRequest change,
        SocialPostRemovalTarget target,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= FilmDocumentUpdate.MaxAttempts; attempt++)
        {
            var latest = await tvShowEpisodeRepository.GetTvShowEpisodeWithETag(firstRead.TvShowId, firstRead.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: TV-show episode '{episodeId}' was deleted during the edit; not saved.",
                    nameof(SaveAdditionalYouTube), firstRead.Id);
                return new TvShowEpisodePersist(new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.NotFound), null);
            }

            var unexpected = YouTubeAlternateChangeApplier.IdsTheClientDidNotList(
                latest.Item.AdditionalYouTube, change.ExpectedAdditionalYouTubeIds);
            if (unexpected.Length > 0)
            {
                logger.LogWarning(
                    "{method}: TV-show episode '{episodeId}' has {count} additional YouTube id(s) this edit did not list; not saved.",
                    nameof(SaveAdditionalYouTube), firstRead.Id, unexpected.Length);
                return new TvShowEpisodePersist(
                    new TvShowEpisodeUpdateResult(
                        TvShowEpisodeUpdateStatus.Conflict,
                        YouTubeAlternateChangeApplier.StaleMessage("TV episode"),
                        UnexpectedAdditionalYouTubeIds: unexpected),
                    null);
            }

            var held = await YouTubeConflict(latest.Item, change, cancellationToken);
            if (held is not null)
            {
                return new TvShowEpisodePersist(new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Conflict, held), null);
            }

            var readIds = (latest.Item.ImdbId, latest.Item.TvdbId, latest.Item.TmdbId);
            if (!PlayableChangeApplier.TryApply(latest.Item, change, out var postStatus, out var error))
            {
                return new TvShowEpisodePersist(new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, error), null);
            }

            CanonicalIdChangeApplier.Apply(latest.Item, change.ImdbId, change.TmdbId, change.TvdbId);
            await FillFromIdentity(
                latest.Item,
                readIds != (latest.Item.ImdbId, latest.Item.TvdbId, latest.Item.TmdbId),
                SkipFrom(change),
                cancellationToken);

            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, latest.Item, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, latest.Item, target)
            };
            if (await tvShowEpisodeRepository.TryReplace(latest.Item, latest.ETag))
            {
                return new TvShowEpisodePersist(
                    new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Accepted, Outcome: outcome),
                    latest.Item);
            }
        }

        throw new InvalidOperationException(
            $"TV-show episode '{firstRead.Id}' kept changing during the edit; not saved.");
    }

    private static IdentityFieldSkip SkipFrom(TvShowEpisodeChangeRequest change) => new(
        Title: !string.IsNullOrWhiteSpace(change.Title),
        Description: change.Description is not null,
        Release: change.Release is not null,
        Length: change.Duration is not null);

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
