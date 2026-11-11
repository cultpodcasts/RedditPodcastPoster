using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// The target side of a move to a TV show, shared by Move to TV show for a podcast episode
/// (<see cref="EpisodeTvShowMoveService"/>) and for a news report (<see cref="News.NewsReportTvShowMoveService"/>), so
/// both validate the body, pick, create, resume and conflict-check a show the same way. Only the source side (which
/// row is read, mapped and deleted) differs between them.
/// </summary>
public static class TvShowMoveTarget
{
    /// <summary>Exactly one of id / name, <c>createShow</c> only by name, <c>publisher</c> only with <c>createShow</c>, no '/'.</summary>
    public static EpisodeTvShowMoveResult? Validate(EpisodeTvShowMoveRequest request)
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
    public static bool Targets(EpisodeTvShowMoveRequest request, TvShowEpisode existing, TvShow? existingShow) =>
        request.TvShowId is { } id && id != Guid.Empty
            ? existing.TvShowId == id
            : string.Equals((existingShow?.Name ?? existing.TvShowName)?.Trim(), request.TvShowName!.Trim(),
                StringComparison.OrdinalIgnoreCase);

    /// <summary>The show an interrupted move already saved the TV episode into, with the same rules as a new target.</summary>
    public static (TvShow? Show, bool Created, EpisodeTvShowMoveResult? Stop) ResumeShow(TvShow? existingShow) =>
        existingShow switch
        {
            null => (null, false, NotFound("TV show not found")),
            { Removed: true } => (null, false, BadRequest("A removed TV show cannot take episodes")),
            _ => (existingShow, false, null)
        };

    /// <summary>
    /// An existing show by id or (unique, case-insensitive) name, or with <c>createShow</c> an unsaved new show with a
    /// fresh id, the publisher, <b>no</b> <c>indexing</c> and a best-effort identity fill. The caller saves a created
    /// show just before the TV episode.
    /// </summary>
    public static async Task<(TvShow? Show, bool Created, EpisodeTvShowMoveResult? Stop)> ResolveShow(
        EpisodeTvShowMoveRequest request,
        ITvShowRepository tvShowRepository,
        IFileKeyAllocator fileKeyAllocator,
        ICatalogueIdentityEnricher identityEnricher,
        ILogger logger,
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
                    "{method}: TV show '{name}' already exists ('{tvShowId}'); the item joins it and the publisher is not applied.",
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

        var showId = Guid.NewGuid();
        var show = new TvShow(name)
        {
            Id = showId,
            FileKey = await fileKeyAllocator.Allocate(FileKeyKind.TvShow, name, showId),
            PublisherName = request.Publisher?.Trim() ?? string.Empty,
            Indexing = null
        };
        await TryFillNewShowIdentity(show, identityEnricher, logger, cancellationToken);
        return (show, true, null);
    }

    /// <summary>
    /// Same confident-match fill as URL submit create. Ambiguous authority results are skipped so the
    /// move still completes (no identity-choice UI on Move to TV); the curator can edit the show later.
    /// </summary>
    private static async Task TryFillNewShowIdentity(
        TvShow show,
        ICatalogueIdentityEnricher identityEnricher,
        ILogger logger,
        CancellationToken cancellationToken)
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
    public static async Task RaiseLatestReleased(
        ITvShowRepository tvShowRepository,
        ILogger logger,
        TvShow show,
        TvShowEpisode tvEpisode)
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

    public static EpisodeTvShowMoveResult Accepted(Guid showId, string? showName, Guid episodeId, bool createdShow) =>
        new(EpisodeTvShowMoveStatus.Accepted, showId, showName?.Trim(), episodeId, createdShow);

    public static EpisodeTvShowMoveResult Conflict(TvShowEpisode existing, string error) =>
        new(EpisodeTvShowMoveStatus.Conflict, existing.TvShowId, existing.TvShowName, existing.Id, Error: error);

    public static EpisodeTvShowMoveResult Failed(TvShow show, TvShowEpisode tvEpisode, bool createdShow) =>
        new(EpisodeTvShowMoveStatus.Failed, show.Id, show.Name, tvEpisode.Id, createdShow, FailureIndexing: true);

    public static EpisodeTvShowMoveResult NotFound(string error) =>
        new(EpisodeTvShowMoveStatus.NotFound, Error: error);

    public static EpisodeTvShowMoveResult BadRequest(string error) =>
        new(EpisodeTvShowMoveStatus.BadRequest, Error: error);
}
