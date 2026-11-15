using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Moves one podcast episode into a TV show ("Move to TV show"), as a <see cref="TvShowEpisode"/> with the same id.
/// The target is an existing show (by id, or by name) or, with <c>createShow</c>, a new show by name. A new show gets
/// a fresh id (never the podcast id), the given publisher, <b>no</b> <c>indexing</c> (so it claims no YouTube
/// channel), and a best-effort IMDb/TMDB/TVDB identity fill when the authority lookup is confident (ambiguous
/// matches leave identity blank — the move dialog has no choice UI). The order mirrors
/// <see cref="EpisodeTrailerAttachService"/>, so every failure stops before the episode row is lost:
/// <list type="number">
/// <item>Save the TV episode (<see cref="CatalogueParentKindMapper.ToTvShowEpisode"/>: services, subjects, posted /
/// tweeted / Bluesky state).</item>
/// <item>Swap the search document under the same id to ContentKind TvShowEpisode. On failure the episode is kept.</item>
/// <item>Re-read the TV episode and the show; if either is gone (a concurrent delete), stop with the episode kept.</item>
/// <item>Delete the episode row.</item>
/// <item>Raise the show's <c>latestReleased</c> with a conditional field patch (no full-document save, so a concurrent
/// curator edit of the show is never overwritten, and a value another writer already raised is never lowered; best
/// effort).</item>
/// <item>Delete the episode's short-link record, as Move to film does (best effort). The Shortner then looks the id up
/// in search (now ContentKind TvShowEpisode) and redirects to <c>/tv/{current show name}/{t-shortId}</c>, which
/// follows show renames.</item>
/// </list>
/// Re-sending resumes: a TV episode with the episode id in the target show while the episode row exists is the
/// interrupted move, so it is not saved twice; with the episode row gone, a re-send is accepted again for that show.
/// The target of a re-send is matched against the show the TV episode is in now (by id, or by its current name), so
/// a show renamed between attempts is not a false conflict.
/// The source podcast is never deleted or changed, even when it has no episodes left. The target side (validation,
/// show resolution, resume and conflict rules, <c>latestReleased</c>) is <see cref="TvShowMoveTarget"/>, shared with
/// Move to TV show for a news report.
/// </summary>
public class EpisodeTvShowMoveService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ICatalogueIdentityEnricher identityEnricher,
    IEpisodeServiceMaterialiser episodeServiceMaterialiser,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<EpisodeTvShowMoveService> logger) : IEpisodeTvShowMoveService
{
    public async Task<EpisodeTvShowMoveResult> MoveAsync(
        EpisodeTvShowMoveCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (TvShowMoveTarget.Validate(request) is { } invalid)
        {
            return invalid;
        }

        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            if (podcast is null)
            {
                return TvShowMoveTarget.NotFound("Podcast not found");
            }

            var episode = await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            var existing = await tvShowEpisodeRepository.GetBy(item => item.Id == command.EpisodeId);
            var existingShow = existing is null ? null : await tvShowRepository.GetTvShow(existing.TvShowId);
            if (episode is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the TV episode has the episode id.
                if (existing is null)
                {
                    return TvShowMoveTarget.NotFound("Episode not found");
                }

                if (!TvShowMoveTarget.Targets(request, existing, existingShow))
                {
                    return TvShowMoveTarget.Conflict(existing, "The episode was already moved to another TV show");
                }

                logger.LogInformation(
                    "{method}: Episode '{episodeId}' was already moved to TV show '{tvShowId}'; accepting the re-send.",
                    nameof(MoveAsync), command.EpisodeId, existing.TvShowId);
                return TvShowMoveTarget.Accepted(existing.TvShowId, existing.TvShowName, existing.Id, createdShow: false);
            }

            if (episode.Removed)
            {
                return TvShowMoveTarget.BadRequest("A removed episode cannot be moved to a TV show");
            }

            if (existing is not null && !TvShowMoveTarget.Targets(request, existing, existingShow))
            {
                return TvShowMoveTarget.Conflict(existing, "The episode id is already a TV episode of another show");
            }

            if (await filmRepository.GetFilm(episode.Id) is { } film)
            {
                logger.LogError(
                    "{method}: Episode '{episodeId}' is still in podcast '{podcastId}' but film '{filmId}' has its id.",
                    nameof(MoveAsync), episode.Id, podcast.Id, film.Id);
                return new EpisodeTvShowMoveResult(
                    EpisodeTvShowMoveStatus.Conflict,
                    Error: "The episode id is already a film. Finish the move to film instead");
            }

            var (show, created, stop) = existing is not null
                ? TvShowMoveTarget.ResumeShow(existingShow)
                : await TvShowMoveTarget.ResolveShow(
                    request, tvShowRepository, fileKeyAllocator, identityEnricher, logger, cancellationToken);
            if (stop is not null)
            {
                return stop;
            }

            TvShowEpisode tvEpisode;
            if (existing is not null)
            {
                logger.LogWarning(
                    "{method}: TV episode '{episodeId}' already exists in show '{tvShowId}' while still in podcast '{podcastId}'; resuming the move.",
                    nameof(MoveAsync), existing.Id, existing.TvShowId, podcast.Id);
                tvEpisode = existing;
            }
            else
            {
                // TV episodes have Services only (no nested ids). Materialise Spotify / Apple / YouTube url + image
                // from ids (clients first) so search/Watch keep every platform after the move.
                await episodeServiceMaterialiser.MaterialiseAsync(podcast, episode, cancellationToken);
                tvEpisode = CatalogueParentKindMapper.ToTvShowEpisode(episode, show!);
                if (!string.IsNullOrWhiteSpace(request.Title))
                {
                    tvEpisode.Title = request.Title.Trim();
                }

                if (created)
                {
                    show!.LatestReleased = tvEpisode.ReleaseSort;
                    await tvShowRepository.Save(show);
                    logger.LogInformation(
                        "{method}: Created TV show '{tvShowName}' ('{tvShowId}') with no indexing for episode '{episodeId}'.",
                        nameof(MoveAsync), show.Name, show.Id, episode.Id);
                }

                await tvShowEpisodeRepository.Save(tvEpisode);
            }

            // Same document shape as the TV episode PATCH and the tv-show-episode pull indexer, so the next pull run
            // does not rewrite it.
            if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(tvEpisode)], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed for TV episode '{episodeId}'. Episode row kept; re-send to resume.",
                    nameof(MoveAsync), tvEpisode.Id);
                return TvShowMoveTarget.Failed(show!, tvEpisode, created);
            }

            // Last check before the only irreversible step: a concurrent delete of the show or the new TV episode
            // would leave the episode with nowhere to live once its row is gone.
            var currentShow = await tvShowRepository.GetTvShow(show!.Id);
            var currentEpisode = await tvShowEpisodeRepository.GetTvShowEpisode(show.Id, tvEpisode.Id);
            if (currentShow is null || currentEpisode is null)
            {
                logger.LogError(
                    "{method}: TV show '{tvShowId}' or TV episode '{episodeId}' is gone (changed concurrently). Episode row kept; re-send to resume.",
                    nameof(MoveAsync), show.Id, tvEpisode.Id);
                return TvShowMoveTarget.Failed(show, tvEpisode, created);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Moved episode '{episodeId}' from podcast '{podcastId}' to TV show '{tvShowName}' ('{tvShowId}').",
                episode.Id, podcast.Id, currentShow.Name, currentShow.Id);
            await TvShowMoveTarget.RaiseLatestReleased(tvShowRepository, logger, currentShow, currentEpisode);
            await DeleteShortLink(podcast, episode);
            return TvShowMoveTarget.Accepted(currentShow.Id, currentShow.Name, currentEpisode.Id, created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to move episode '{episodeId}' of podcast '{podcastId}' to a TV show.",
                nameof(MoveAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeTvShowMoveResult(EpisodeTvShowMoveStatus.Failed);
        }
    }

    /// <summary>
    /// The short-link key is the episode id (base 64), which the TV episode keeps. As in Move to film, the record is
    /// deleted: with no record the Shortner looks the id up in search (now ContentKind TvShowEpisode, seriesName = the
    /// show name) and redirects to <c>/tv/{show}/{t-shortId}</c>, so the link follows a later show rename. The move
    /// has already succeeded, so a failure is logged and does not fail the request.
    /// </summary>
    private async Task DeleteShortLink(Podcast podcast, Episode episode)
    {
        try
        {
            var result = await shortnerService.Delete(new PodcastEpisode(podcast, episode));
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short-link record for '{episodeId}' was not deleted (absent, or the KV delete failed).",
                    nameof(DeleteShortLink), episode.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link delete failed for moved episode '{episodeId}'.",
                nameof(DeleteShortLink), episode.Id);
        }
    }
}
