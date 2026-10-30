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
/// The source podcast is never deleted or changed, even when it has no episodes left.
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
    ILogger<EpisodeTvShowMoveService> logger) : IEpisodeTvShowMoveService
{
    public async Task<EpisodeTvShowMoveResult> MoveAsync(
        EpisodeTvShowMoveCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            if (podcast is null)
            {
                return NotFound("Podcast not found");
            }

            var episode = await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            var existing = await tvShowEpisodeRepository.GetBy(item => item.Id == command.EpisodeId);
            var existingShow = existing is null ? null : await tvShowRepository.GetTvShow(existing.TvShowId);
            if (episode is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the TV episode has the episode id.
                if (existing is null)
                {
                    return NotFound("Episode not found");
                }

                if (!Targets(request, existing, existingShow))
                {
                    return Conflict(existing, "The episode was already moved to another TV show");
                }

                logger.LogInformation(
                    "{method}: Episode '{episodeId}' was already moved to TV show '{tvShowId}'; accepting the re-send.",
                    nameof(MoveAsync), command.EpisodeId, existing.TvShowId);
                return Accepted(existing.TvShowId, existing.TvShowName, existing.Id, createdShow: false);
            }

            if (episode.Removed)
            {
                return BadRequest("A removed episode cannot be moved to a TV show");
            }

            if (existing is not null && !Targets(request, existing, existingShow))
            {
                return Conflict(existing, "The episode id is already a TV episode of another show");
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
                ? ResumeShow(existingShow)
                : await ResolveShow(request, cancellationToken);
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
                return Failed(show!, tvEpisode, created);
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
                return Failed(show, tvEpisode, created);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Moved episode '{episodeId}' from podcast '{podcastId}' to TV show '{tvShowName}' ('{tvShowId}').",
                episode.Id, podcast.Id, currentShow.Name, currentShow.Id);
            await RaiseLatestReleased(currentShow, currentEpisode);
            await DeleteShortLink(podcast, episode);
            return Accepted(currentShow.Id, currentShow.Name, currentEpisode.Id, created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to move episode '{episodeId}' of podcast '{podcastId}' to a TV show.",
                nameof(MoveAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeTvShowMoveResult(EpisodeTvShowMoveStatus.Failed);
        }
    }

    private static EpisodeTvShowMoveResult? Validate(EpisodeTvShowMoveRequest request)
    {
        var hasId = request.TvShowId is { } id && id != Guid.Empty;
        var hasName = !string.IsNullOrWhiteSpace(request.TvShowName);
        if (hasId == hasName)
        {
            return BadRequest("Give exactly one of tvShowId or tvShowName");
        }

        if (request.CreateShow && !hasName)
        {
            return BadRequest("createShow needs tvShowName");
        }

        if (!string.IsNullOrWhiteSpace(request.Publisher) && !request.CreateShow)
        {
            return BadRequest("publisher only applies to a new show (createShow). Use Edit TV show to change a show's publisher");
        }

        if (hasName && request.TvShowName!.Contains('/'))
        {
            return BadRequest("TV show name cannot contain '/'");
        }

        return null;
    }

    /// <summary>
    /// True when the request names the show the TV episode is in: by id, or by the show's <b>current</b> name
    /// case-insensitively (the TV episode's denormalised name only when the show is gone), so a rename between
    /// attempts is not a false conflict.
    /// </summary>
    private static bool Targets(EpisodeTvShowMoveRequest request, TvShowEpisode existing, TvShow? existingShow) =>
        request.TvShowId is { } id && id != Guid.Empty
            ? existing.TvShowId == id
            : string.Equals((existingShow?.Name ?? existing.TvShowName)?.Trim(), request.TvShowName!.Trim(),
                StringComparison.OrdinalIgnoreCase);

    /// <summary>The show an interrupted move already saved the TV episode into, with the same rules as a new target.</summary>
    private static (TvShow? Show, bool Created, EpisodeTvShowMoveResult? Stop) ResumeShow(TvShow? existingShow) =>
        existingShow switch
        {
            null => (null, false, NotFound("TV show not found")),
            { Removed: true } => (null, false, BadRequest("A removed TV show cannot take episodes")),
            _ => (existingShow, false, null)
        };

    private async Task<(TvShow? Show, bool Created, EpisodeTvShowMoveResult? Stop)> ResolveShow(
        EpisodeTvShowMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TvShowId is { } id && id != Guid.Empty)
        {
            var byId = await tvShowRepository.GetTvShow(id);
            if (byId is null)
            {
                return (null, false, NotFound("TV show not found"));
            }

            return byId.Removed == true
                ? (null, false, BadRequest("A removed TV show cannot take episodes"))
                : (byId, false, null);
        }

        var name = request.TvShowName!.Trim();
        var matches = await PublisherNameAttachLookup.FindByName(tvShowRepository, name, cancellationToken);
        if (matches.Count > 1)
        {
            logger.LogError("{method}: {count} TV shows are named '{name}'.", nameof(ResolveShow), matches.Count, name);
            return (null, false, new EpisodeTvShowMoveResult(
                EpisodeTvShowMoveStatus.Conflict,
                Error: "Several TV shows have this name. Pick the show by id"));
        }

        if (matches.Count == 1)
        {
            var match = matches[0];
            if (request.CreateShow)
            {
                logger.LogInformation(
                    "{method}: TV show '{name}' already exists ('{tvShowId}'); the episode joins it and the publisher is not applied.",
                    nameof(ResolveShow), match.Name, match.Id);
            }

            return match.Removed == true
                ? (null, false, BadRequest("A removed TV show cannot take episodes"))
                : (match, false, null);
        }

        if (!request.CreateShow)
        {
            return (null, false, NotFound("No TV show has this name"));
        }

        var show = new TvShow(name)
        {
            Id = Guid.NewGuid(),
            FileKey = FileKeyFactory.GetTvShowFileKey(name),
            PublisherName = request.Publisher?.Trim() ?? string.Empty,
            Indexing = null
        };
        await TryFillNewShowIdentity(show, cancellationToken);
        return (show, true, null);
    }

    /// <summary>
    /// Same confident-match fill as URL submit create. Ambiguous authority results are skipped so the
    /// move still completes (no identity-choice UI on Move to TV); the curator can edit the show later.
    /// </summary>
    private async Task TryFillNewShowIdentity(TvShow show, CancellationToken cancellationToken)
    {
        try
        {
            var admission = await identityEnricher.ResolveTvShowAsync(
                show.Name,
                CatalogueIdentityChoice.Unset,
                selectedKey: null,
                cancellationToken);
            if (admission.Hold)
            {
                logger.LogInformation(
                    "{method}: TV show '{name}' identity is ambiguous ({count} candidates); leaving blank.",
                    nameof(TryFillNewShowIdentity),
                    show.Name,
                    admission.Prompt?.Candidates.Count ?? 0);
                return;
            }

            if (identityEnricher.ApplyMissing(show, admission.Ids))
            {
                logger.LogInformation(
                    "{method}: Filled identity for new TV show '{name}' (imdb={imdbId}, tmdb={tmdbId}, tvdb={tvdbId}).",
                    nameof(TryFillNewShowIdentity),
                    show.Name,
                    show.ImdbId,
                    show.TmdbId,
                    show.TvdbId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "{method}: Identity lookup failed for new TV show '{name}'; leaving blank.",
                nameof(TryFillNewShowIdentity),
                show.Name);
        }
    }

    /// <summary>
    /// Patches only <c>/latestReleased</c> (the Indexer-owned field), so the show's curator fields are never written,
    /// and only when the stored value is still older (a conditional patch), so a second move into the same show or
    /// another writer that raised it meanwhile is never lowered. The move has already succeeded, so a failure is
    /// logged and does not fail the request.
    /// </summary>
    private async Task RaiseLatestReleased(TvShow show, TvShowEpisode tvEpisode)
    {
        var released = tvEpisode.ReleaseSort;
        if (released == default || (show.LatestReleased is { } latest && latest >= released))
        {
            return;
        }

        try
        {
            if (!await tvShowRepository.RaiseLatestReleased(show.Id, released))
            {
                logger.LogWarning("{method}: TV show '{tvShowId}' was not found to patch latestReleased.",
                    nameof(RaiseLatestReleased), show.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: latestReleased patch failed for TV show '{tvShowId}'.",
                nameof(RaiseLatestReleased), show.Id);
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

    private static EpisodeTvShowMoveResult Accepted(Guid showId, string? showName, Guid episodeId, bool createdShow) =>
        new(EpisodeTvShowMoveStatus.Accepted, showId, showName?.Trim(), episodeId, createdShow);

    private static EpisodeTvShowMoveResult Conflict(TvShowEpisode existing, string error) =>
        new(EpisodeTvShowMoveStatus.Conflict, existing.TvShowId, existing.TvShowName, existing.Id, Error: error);

    private static EpisodeTvShowMoveResult Failed(TvShow show, TvShowEpisode tvEpisode, bool createdShow) =>
        new(EpisodeTvShowMoveStatus.Failed, show.Id, show.Name, tvEpisode.Id, createdShow, FailureIndexing: true);

    private static EpisodeTvShowMoveResult NotFound(string error) =>
        new(EpisodeTvShowMoveStatus.NotFound, Error: error);

    private static EpisodeTvShowMoveResult BadRequest(string error) =>
        new(EpisodeTvShowMoveStatus.BadRequest, Error: error);
}
