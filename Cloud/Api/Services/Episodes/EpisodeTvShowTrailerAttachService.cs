using Api.Models;
using Api.Services.TvShows;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Attaches one podcast episode to a TV show as a trailer ("Attach as TV show trailer", RPP #1412): an existing show by
/// id, or with <c>createShow</c> the show named <c>tvShowName</c>, created first when no show has the name (a show with
/// no episodes and no indexing; see <see cref="ResolveNamedShow"/>). Mirrors <see cref="EpisodeTrailerAttachService"/>
/// for films, with three differences:
/// <list type="bullet">
/// <item>A TV show has <b>no search document</b>, so no show document is pushed. Only the episode's own search
/// document is deleted.</item>
/// <item>The attach never touches the show's <c>latestReleased</c> (a trailer is not a TV episode, unlike Move to TV
/// show, which calls <c>RaiseLatestReleased</c>).</item>
/// <item>The short link points at the TV show page (<c>/tv/{name}</c>).</item>
/// </list>
/// The trailer lives on the show document only, so the episode stops being a playable. The order is chosen so every
/// failure stops before anything is lost:
/// <list type="number">
/// <item>Claim the episode for the show in LookUps (<see cref="ITvShowTrailerClaimStore"/>, a conditional create). Of
/// two attaches of one episode to different shows exactly one wins; the other stops with 409 before writing any show,
/// so nothing is ever undone and no show is deleted.</item>
/// <item>Add a <see cref="TvShowTrailer"/> (id = episode id) to the show and save it, ETag-guarded (re-read on a
/// conflict). Before the add, shows are queried across partitions for that id in <c>trailers[].id</c>. Another show
/// that already holds it returns 409 naming that show and the episode stays.</item>
/// <item>Delete the episode's search document. On failure the episode is kept.</item>
/// <item>Re-read the show. If it was deleted, best-effort re-upload the episode search document and stop with the
/// episode kept. If it no longer holds the trailer (a concurrent write dropped it), stop with the episode kept.</item>
/// <item>Delete the episode row.</item>
/// <item>Point the episode's short link at the TV show page (best effort).</item>
/// </list>
/// Re-sending resumes: a show that already holds the trailer while the episode row exists is the interrupted attach, so
/// the trailer is not added twice; with the episode row gone, a re-send is accepted again for that show.
/// The source podcast is never deleted or changed.
/// </summary>
public class EpisodeTvShowTrailerAttachService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ICatalogueIdentityEnricher identityEnricher,
    IFileKeyAllocator fileKeyAllocator,
    ITvShowTrailerClaimStore trailerClaims,
    TimeProvider timeProvider,
    ILogger<EpisodeTvShowTrailerAttachService> logger) : IEpisodeTvShowTrailerAttachService
{
    /// <summary>
    /// A claim whose show does not hold the trailer is an attach still running (seconds) or one that failed. Only a
    /// claim older than this is taken over.
    /// </summary>
    internal static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(10);

    private const int MaxSaveAttempts = 3;

    public async Task<EpisodeTvShowTrailerAttachResult> AttachAsync(
        EpisodeTvShowTrailerAttachCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (TvShowMoveTarget.Validate(request.ToTarget()) is { } invalid)
        {
            return BadRequest(invalid.Error ?? "Invalid request");
        }

        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            if (podcast is null)
            {
                return new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.NotFound, Error: "Podcast not found");
            }

            var episode = await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            TvShow? show;
            var showCreated = false;
            if (request.CreateShow)
            {
                var (target, created, stop) = await ResolveNamedShow(command, episode, cancellationToken);
                if (stop is not null)
                {
                    return stop;
                }

                show = target;
                showCreated = created;
            }
            else if (request.TvShowId is { } id && id != Guid.Empty)
            {
                show = await tvShowRepository.GetTvShow(id);
            }
            else
            {
                (show, var stop) = await FindShowByName(request.TvShowName!, cancellationToken);
                if (stop is not null)
                {
                    return stop;
                }
            }

            if (episode is null)
            {
                // A re-send after a completed attach (double click, or a lost 202): the show holds the trailer.
                if (show?.HasTrailer(command.EpisodeId) == true)
                {
                    logger.LogInformation(
                        "{method}: Episode '{episodeId}' is already a trailer of TV show '{tvShowId}'; accepting the re-send.",
                        nameof(AttachAsync), command.EpisodeId, show.Id);
                    return new EpisodeTvShowTrailerAttachResult(
                        EpisodeTvShowTrailerAttachStatus.Accepted, show.Id, show.Name);
                }

                return new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.NotFound, Error: "Episode not found");
            }

            if (show is null)
            {
                return new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.NotFound, Error: "TV show not found");
            }

            if (episode.Removed)
            {
                return BadRequest("A removed episode cannot be attached as a trailer");
            }

            if (show.IsRemoved())
            {
                // A removed show's page is gone; a trailer there would be invisible.
                return BadRequest("A removed TV show cannot take trailers");
            }

            if (await RowWithEpisodeId(episode.Id) is { } clash)
            {
                return clash;
            }

            // A show created by this call already holds the trailer (it was in the show's only write).
            if (show.HasTrailer(episode.Id) && !showCreated)
            {
                logger.LogWarning(
                    "{method}: TV show '{tvShowId}' already holds trailer '{episodeId}' still in podcast '{podcastId}'; resuming.",
                    nameof(AttachAsync), show.Id, episode.Id, podcast.Id);
            }
            else if (!show.HasTrailer(episode.Id))
            {
                var trailer = TvShowTrailerFactory.From(episode, request.Title);
                if (trailer is null)
                {
                    return BadRequest("The episode has no link to play the trailer from");
                }

                var claimStop = await ClaimTrailer(episode.Id, show.Id);
                if (claimStop is not null)
                {
                    return claimStop;
                }

                var (saved, stop) = await AddTrailer(show.Id, trailer);
                if (stop is not null)
                {
                    return stop;
                }

                show = saved!;
            }

            // No show search document exists to push: only the episode's own document leaves search.
            if (!await searchSwap.DeleteAsync([episode.Id], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search delete failed for episode '{episodeId}'. Episode row kept; re-send to resume.",
                    nameof(AttachAsync), episode.Id);
                return Failed(show);
            }

            // Last check before the only irreversible step: another show write since our save (one that read the show
            // before it) would have dropped the trailer, and deleting the episode then would lose it for good.
            var current = await tvShowRepository.GetTvShow(show.Id);
            if (current is null)
            {
                // The episode search document is already gone. A re-send finds no show and returns 404, so nothing
                // else puts the episode back into search. Restore it now; the episode row stays.
                logger.LogWarning(
                    "TvShowTrailerAttachShowGone: TV show '{tvShowId}' was deleted after the trailer save. Re-uploading the search document for episode '{episodeId}'.",
                    show.Id, episode.Id);
                await RestoreEpisodeSearchDocument(podcast, episode, show.Id, cancellationToken);
                return Failed(show);
            }

            if (!current.HasTrailer(episode.Id))
            {
                logger.LogError(
                    "{method}: TV show '{tvShowId}' no longer holds trailer '{episodeId}' (changed concurrently). Episode row kept; re-send to resume.",
                    nameof(AttachAsync), show.Id, episode.Id);
                // Search already lost the episode, so report it as an indexing failure: the client prompts a re-send,
                // which adds the trailer again and puts the episode's removal from search back in step.
                return Failed(show);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Attached episode '{episodeId}' of podcast '{podcastId}' to TV show '{tvShowId}' as a trailer.",
                episode.Id, podcast.Id, show.Id);
            await RedirectShortLink(podcast, episode, current);
            return new EpisodeTvShowTrailerAttachResult(
                EpisodeTvShowTrailerAttachStatus.Accepted, current.Id, current.Name, ShowCreated: showCreated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to attach episode '{episodeId}' of podcast '{podcastId}' to a TV show as a trailer.",
                nameof(AttachAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeTvShowTrailerAttachResult(EpisodeTvShowTrailerAttachStatus.Failed);
        }
    }

    /// <summary>
    /// The episode id is also a TV episode or a film id when a Move to TV show or Move to film was interrupted. Deleting
    /// the episode's search document would then delete that item's document (same id), so the attach stops.
    /// </summary>
    private async Task<EpisodeTvShowTrailerAttachResult?> RowWithEpisodeId(Guid episodeId)
    {
        if (await tvShowEpisodeRepository.GetBy(item => item.Id == episodeId) is { } tvEpisode)
        {
            return Conflict(tvEpisode.TvShowId, "The episode id is already a TV episode. Finish the move to TV show instead");
        }

        if (await filmRepository.GetFilm(episodeId) is { } film)
        {
            return Conflict(null, $"The episode id is already a film ('{film.Id}'). Finish the move to film instead");
        }

        return null;
    }

    /// <summary>The show named <paramref name="name"/> (without <c>createShow</c>): unique, case-insensitive.</summary>
    private async Task<(TvShow? Show, EpisodeTvShowTrailerAttachResult? Stop)> FindShowByName(
        string name,
        CancellationToken cancellationToken)
    {
        var matches = await PublisherNameAttachLookup.FindByName(tvShowRepository, name.Trim(), cancellationToken);
        return matches.Count switch
        {
            0 => (null, new EpisodeTvShowTrailerAttachResult(
                EpisodeTvShowTrailerAttachStatus.NotFound, Error: "No TV show has this name")),
            1 => (matches[0], null),
            _ => (null, Conflict(matches[0].Id, "Several TV shows have this name. Pick the show by id"))
        };
    }

    /// <summary>
    /// The <c>createShow</c> target: the show named <c>tvShowName</c>, created when no show has the name. Every episode
    /// check that can stop the attach runs first, so a refused attach never leaves a new, empty show behind.
    /// <list type="bullet">
    /// <item>A show that already holds this trailer is the interrupted (or completed) attach: it is reused when its name
    /// matches, and is a 409 naming that show otherwise.</item>
    /// <item>One show with the name (case-insensitive) is reused, as Move to TV show's <c>createShow</c> joins an existing
    /// show and ignores the publisher. More than one is a 409.</item>
    /// <item>Otherwise the new show is built as <see cref="TvShowMoveTarget"/> builds one for a move: the given
    /// publisher, <b>no</b> <c>indexing</c> (so it claims no YouTube channel), and a best-effort IMDb/TMDB/TVDB identity
    /// fill when the authority lookup is confident (ambiguous matches leave identity blank).</item>
    /// <item>The new show is written once, already holding the trailer, with a conditional create under an id derived
    /// from the name (<see cref="NewTvShowId"/>). Overlapping creates of the same name converge on that one document (the
    /// loser re-reads it and resumes), and no empty show is ever stored: any later failure leaves a show holding the
    /// trailer with the episode row kept, which a re-send resumes.</item>
    /// </list>
    /// Re-sending after a failure finds the show by its trailer or its name, so a show is never created twice.
    /// </summary>
    private async Task<(TvShow? Show, bool Created, EpisodeTvShowTrailerAttachResult? Stop)> ResolveNamedShow(
        EpisodeTvShowTrailerAttachCommand command,
        Episode? episode,
        CancellationToken cancellationToken)
    {
        var name = command.Request.TvShowName!.Trim();
        var holder = await FirstShowHoldingTrailer(command.EpisodeId);
        if (episode is null)
        {
            // A re-send after a completed attach: the episode row is gone and the show holds the trailer.
            return holder is not null
                ? (null, false, new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.Accepted, holder.Id, holder.Name))
                : (null, false, new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.NotFound, Error: "Episode not found"));
        }

        if (episode.Removed)
        {
            return (null, false, BadRequest("A removed episode cannot be attached as a trailer"));
        }

        var trailer = TvShowTrailerFactory.From(episode, command.Request.Title);
        if (trailer is null)
        {
            return (null, false, BadRequest("The episode has no link to play the trailer from"));
        }

        if (holder is not null)
        {
            return string.Equals(holder.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)
                ? (holder, false, null)
                : (null, false, Conflict(holder.Id, "This episode is already a trailer of another TV show"));
        }

        if (await RowWithEpisodeId(episode.Id) is { } clash)
        {
            return (null, false, clash);
        }

        var matches = await PublisherNameAttachLookup.FindByName(tvShowRepository, name, cancellationToken);
        if (matches.Count == 1)
        {
            logger.LogInformation(
                "{method}: TV show '{tvShowId}' already has the name '{name}'; attaching the trailer to it.",
                nameof(ResolveNamedShow), matches[0].Id, name);
            return (matches[0], false, null);
        }

        if (matches.Count > 1)
        {
            return (null, false, Conflict(matches[0].Id, "Several TV shows have this name. Pick the show by id"));
        }

        // The owner check above is only a query. The claim is the one conditional write that decides which show gets
        // the episode; a call that loses it stops here, before writing anything else.
        var showId = NewTvShowId(name);
        var claimStop = await ClaimTrailer(episode.Id, showId);
        if (claimStop is not null)
        {
            return (null, false, claimStop);
        }

        var show = new TvShow(name)
        {
            Id = showId,
            PublisherName = command.Request.Publisher?.Trim() ?? string.Empty,
            Indexing = null,
            Trailers = [trailer]
        };
        show.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.TvShow, name, showId);
        await TvShowMoveTarget.TryFillNewShowIdentity(show, identityEnricher, logger, cancellationToken);

        if (!await tvShowRepository.TryCreate(show))
        {
            return await ResolveCreateConflict(show.Id, name);
        }

        logger.LogInformation(
            "{method}: Created TV show '{tvShowId}' '{name}' (imdb={imdbId}, tmdb={tmdbId}, tvdb={tvdbId}) holding trailer '{episodeId}'.",
            nameof(ResolveNamedShow), show.Id, show.Name, show.ImdbId, show.TmdbId, show.TvdbId, episode.Id);

        return (show, true, null);
    }

    /// <summary>
    /// Claims the episode as a trailer of <paramref name="tvShowId"/> (see <see cref="ITvShowTrailerClaimStore"/>). Null
    /// when this show owns it; otherwise a 409 naming the show that does. Called before the first write of the trailer
    /// to any show, so a loser writes nothing and nothing has to be undone.
    /// </summary>
    private async Task<EpisodeTvShowTrailerAttachResult?> ClaimTrailer(Guid episodeId, Guid tvShowId)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var attempt = await trailerClaims.TryClaim(episodeId, tvShowId, now);
        if (attempt.Claimed)
        {
            return null;
        }

        var holder = attempt.Holder!;
        var holderShow = await tvShowRepository.GetTvShow(holder.TvShowId);
        if (holderShow?.HasTrailer(episodeId) == true)
        {
            return Conflict(holder.TvShowId, "This episode is already a trailer of another TV show");
        }

        if (now - holder.ClaimedUtc < ClaimStaleAfter || attempt.HolderETag is null ||
            !await trailerClaims.TryTakeOver(episodeId, tvShowId, now, attempt.HolderETag))
        {
            logger.LogWarning(
                "{method}: Episode '{episodeId}' is claimed as a trailer of TV show '{holderTvShowId}' since {claimedUtc:O}; refusing TV show '{tvShowId}'.",
                nameof(ClaimTrailer), episodeId, holder.TvShowId, holder.ClaimedUtc, tvShowId);
            return Conflict(holder.TvShowId, "Another attach of this episode as a trailer is in progress; try again shortly");
        }

        return null;
    }

    /// <summary>
    /// Another call created the show for this name first (or the name's id belongs to a show renamed since). The same
    /// name goes on through the normal attach, which resumes when that show already holds the trailer.
    /// </summary>
    private async Task<(TvShow? Show, bool Created, EpisodeTvShowTrailerAttachResult? Stop)> ResolveCreateConflict(
        Guid tvShowId,
        string name)
    {
        var existing = await tvShowRepository.GetTvShow(tvShowId);
        if (existing is null)
        {
            return (null, false, new EpisodeTvShowTrailerAttachResult(
                EpisodeTvShowTrailerAttachStatus.Failed, Error: "The TV show changed while it was created; send again"));
        }

        if (!string.Equals(existing.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
        {
            return (null, false, Conflict(existing.Id,
                $"A TV show first created as '{name}' is now named '{existing.Name}'; attach to it by id or choose another name"));
        }

        logger.LogInformation(
            "{method}: TV show '{tvShowId}' '{name}' was created by another request; attaching to it.",
            nameof(ResolveCreateConflict), existing.Id, existing.Name);
        return (existing, false, null);
    }

    /// <summary>
    /// The id of a show created by name: a name-based GUID (RFC 4122 version 5 layout over SHA-256) of the trimmed,
    /// lower-case name, so every create of the same name targets the same document. The namespace string differs from
    /// the film one, so a film and a show of one name never share an id.
    /// </summary>
    internal static Guid NewTvShowId(string name)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("cultpodcasts:tvshow-by-name:" + name.Trim().ToLowerInvariant()));
        var id = bytes[..16];
        id[6] = (byte)((id[6] & 0x0F) | 0x50);
        id[8] = (byte)((id[8] & 0x3F) | 0x80);
        return new Guid(id, bigEndian: true);
    }

    private async Task<TvShow?> FirstShowHoldingTrailer(Guid trailerId)
    {
        foreach (var tvShowId in await tvShowRepository.GetTvShowIdsByTrailerId(trailerId))
        {
            if (await tvShowRepository.GetTvShow(tvShowId) is { } show)
            {
                return show;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds the trailer with an ETag-guarded replace, so a show write that lands between our read and our save (another
    /// attach, a show edit) is never overwritten: on a conflict the show is re-read and the trailer added again.
    /// </summary>
    private async Task<(TvShow? Show, EpisodeTvShowTrailerAttachResult? Stop)> AddTrailer(
        Guid tvShowId,
        TvShowTrailer trailer)
    {
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var version = await tvShowRepository.GetTvShowForUpdate(tvShowId);
            if (version is null)
            {
                return (null, new EpisodeTvShowTrailerAttachResult(
                    EpisodeTvShowTrailerAttachStatus.NotFound, Error: "TV show not found"));
            }

            var latest = version.TvShow;
            if (latest.IsRemoved())
            {
                return (null, BadRequest("A removed TV show cannot take trailers"));
            }

            if (latest.HasTrailer(trailer.Id))
            {
                return (latest, null);
            }

            var otherShowId = await OtherShowHoldingTrailer(tvShowId, trailer.Id);
            if (otherShowId is Guid holdingShowId)
            {
                logger.LogWarning(
                    "{method}: Episode '{episodeId}' is already a trailer of TV show '{otherTvShowId}'; refusing to attach it to TV show '{tvShowId}'.",
                    nameof(AddTrailer), trailer.Id, holdingShowId, tvShowId);
                return (null, Conflict(holdingShowId, "This episode is already a trailer of another TV show"));
            }

            latest.Trailers ??= [];
            latest.Trailers.Add(trailer);
            if (await tvShowRepository.TryReplace(latest, version.ETag))
            {
                return (latest, null);
            }

            logger.LogWarning(
                "{method}: TV show '{tvShowId}' changed while adding trailer '{trailerId}' (attempt {attempt} of {max}); re-reading.",
                nameof(AddTrailer), tvShowId, trailer.Id, attempt, MaxSaveAttempts);
        }

        logger.LogError(
            "{method}: TV show '{tvShowId}' kept changing; trailer '{trailerId}' not added. Episode row kept; re-send to resume.",
            nameof(AddTrailer), tvShowId, trailer.Id);
        return (null, new EpisodeTvShowTrailerAttachResult(EpisodeTvShowTrailerAttachStatus.Failed, tvShowId));
    }

    /// <summary>
    /// A show other than <paramref name="tvShowId"/> that already lists <paramref name="trailerId"/>.
    /// The destination show is ignored: re-sending to the show that already holds the trailer still resumes.
    /// </summary>
    private async Task<Guid?> OtherShowHoldingTrailer(Guid tvShowId, Guid trailerId)
    {
        foreach (var holder in await tvShowRepository.GetTvShowIdsByTrailerId(trailerId))
        {
            if (holder != tvShowId)
            {
                return holder;
            }
        }

        return null;
    }

    /// <summary>
    /// The show disappeared after the episode's search document was deleted. Putting the episode document back is best
    /// effort: a failure is logged and the attach still stops with the episode row kept.
    /// </summary>
    private async Task RestoreEpisodeSearchDocument(
        Podcast podcast,
        Episode episode,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();
            if (await searchSwap.UploadAsync([document], cancellationToken))
            {
                return;
            }

            logger.LogError(
                "TvShowTrailerAttachShowGone: Search re-upload failed for episode '{episodeId}' of deleted TV show '{tvShowId}'.",
                episode.Id, tvShowId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TvShowTrailerAttachShowGone: Search re-upload failed for episode '{episodeId}' of deleted TV show '{tvShowId}'.",
                episode.Id, tvShowId);
        }
    }

    private static EpisodeTvShowTrailerAttachResult BadRequest(string error) =>
        new(EpisodeTvShowTrailerAttachStatus.BadRequest, Error: error);

    private static EpisodeTvShowTrailerAttachResult Conflict(Guid? holdingTvShowId, string error) =>
        new(EpisodeTvShowTrailerAttachStatus.Conflict, holdingTvShowId, Error: error);

    private static EpisodeTvShowTrailerAttachResult Failed(TvShow show) =>
        new(EpisodeTvShowTrailerAttachStatus.Failed, show.Id, show.Name, FailureIndexing: true);

    /// <summary>
    /// The short-link key is the episode id (base 64). The id leaves search, so deleting the record would break the
    /// link; instead the record points at the TV show page (<c>/tv/{name}</c>). The attach has already succeeded, so a
    /// failure is logged and does not fail the request.
    /// </summary>
    private async Task RedirectShortLink(Podcast podcast, Episode episode, TvShow show)
    {
        try
        {
            var result = await shortnerService.Redirect(
                new PodcastEpisode(podcast, episode), CatalogueShortId.TvShowPath(show.Name.Trim()));
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short link for trailer '{episodeId}' was not pointed at TV show '{tvShowId}'.",
                    nameof(RedirectShortLink), episode.Id, show.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link redirect failed for trailer '{episodeId}'.",
                nameof(RedirectShortLink), episode.Id);
        }
    }
}
