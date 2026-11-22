using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Moves one podcast episode into a new Film, the way the retired catalogue migration moved a one-episode podcast to a film:
/// <list type="number">
/// <item>Map with <see cref="CatalogueParentKindMapper.ToFilm"/>, so the film keeps the episode id, services, subjects,
/// and posted / tweeted / Bluesky state. The name is the episode title unless the request gives one.</item>
/// <item>Write the film, then swap the search document under the same id to ContentKind Film
/// (<see cref="CatalogueSearchDocuments"/>), then delete the source episode row.</item>
/// </list>
/// As in kind transfer, a failed search swap stops before the source row is deleted. Re-sending the request then
/// resumes: a film with the episode id while the episode row still exists is the interrupted move, so the film is
/// reused rather than reported as a conflict.
/// After the episode row is deleted, the episode's short-link record is deleted (best effort), as catalogue edits do,
/// so the Shortner falls back to search and redirects the old short link straight to the film page. Re-sending after
/// a completed move (episode row gone, film with the episode id present) is accepted again for that film.
/// A newly created film resolves IMDb/TMDB like URL-submit film create: confident match fills ids; ambiguous returns
/// <see cref="EpisodeFilmMoveStatus.IdentityDecisionRequired"/> with candidates (nothing written) so the client can
/// <c>select</c> / <c>continue</c> / <c>cancel</c> on a re-POST — same contract as send-podcast / Identity-tab lookup.
/// The source podcast is never deleted or changed, even when it has no episodes left. The new-film checks (name, clash,
/// identity gate) are <see cref="FilmMoveTarget"/>, shared with Move to film for a news report.
/// With <c>filmId</c> in the request the episode is merged into that existing film instead (RPP #1522, see
/// <see cref="MergeIntoFilmAsync"/>).
/// </summary>
public class EpisodeFilmMoveService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ICatalogueIdentityEnricher identityEnricher,
    IIdentityMetadataEnricher identityMetadata,
    IEpisodeServiceMaterialiser episodeServiceMaterialiser,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<EpisodeFilmMoveService> logger) : IEpisodeFilmMoveService
{
    public async Task<EpisodeFilmMoveResult> MoveAsync(EpisodeFilmMoveCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            var episode = podcast is null
                ? null
                : await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            if (podcast is null)
            {
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound);
            }

            if (command.Request.FilmId is { } targetFilmId && targetFilmId != Guid.Empty)
            {
                return await MergeIntoFilmAsync(podcast, episode, targetFilmId, cancellationToken);
            }

            if (episode is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the film has the episode id.
                var moved = await filmRepository.GetFilm(command.EpisodeId);
                if (moved is null)
                {
                    return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound);
                }

                logger.LogInformation(
                    "{method}: Episode '{episodeId}' was already moved to film '{filmId}'; accepting the re-send.",
                    nameof(MoveAsync), command.EpisodeId, moved.Id);
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, moved.Id, moved.Name);
            }

            if (episode.Removed)
            {
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.BadRequest,
                    Error: "A removed episode cannot be moved to a film");
            }

            // #1484 / #1416: a split-off stays hidden until a curator approves it.
            if (episode.IsHeldForApproval())
            {
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.BadRequest,
                    Error: "An episode held for approval cannot be moved to a film");
            }

            var film = await filmRepository.GetFilm(episode.Id);
            if (film is not null)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' already exists for episode still in podcast '{podcastId}'; resuming the move.",
                    nameof(MoveAsync), film.Id, podcast.Id);
            }
            else
            {
                var (stop, name, admission) = await FilmMoveTarget.PrepareNewFilm(
                    episode.Title, episode.Release, command.Request, filmRepository, identityEnricher, logger,
                    cancellationToken);
                if (stop is not null)
                {
                    return stop;
                }

                // Films keep Services only (no nested ids). Materialise Spotify / Apple / YouTube url + image
                // from ids (clients first) so search/Watch keep every platform after the move.
                await episodeServiceMaterialiser.MaterialiseAsync(podcast, episode, cancellationToken);
                film = CatalogueParentKindMapper.ToFilm(podcast, episode);
                film.Name = name;
                film.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.Film, name, film.Id);
                // ToFilm copies publisher fields from the podcast, including Removed. Here the (active) episode is the
                // unit that moves, so the film is active even when it leaves a removed podcast.
                film.Removed = null;

                FilmMoveTarget.ApplyIdentity(film, admission, identityEnricher, logger);
                await FilmMoveTarget.ApplyIdentityMetadataAsync(film, identityMetadata, cancellationToken);

                await filmRepository.Save(film);
            }

            // Same document shape as the film PATCH and the film pull indexer, so the next pull run does not rewrite it.
            var documents = new[] { CataloguePlayableSearchDocument.For(film) };
            if (!await searchSwap.UploadAsync(documents, cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed for film '{filmId}'. Episode row kept; re-send to resume.",
                    nameof(MoveAsync), film.Id);
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Moved episode '{episodeId}' from podcast '{podcastId}' to film '{filmName}'.",
                episode.Id, podcast.Id, film.Name);
            await DeleteShortLink(podcast, episode);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, film.Id, film.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to move episode '{episodeId}' of podcast '{podcastId}' to a film.",
                nameof(MoveAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed);
        }
    }

    private const int MaxMergeSaveAttempts = 3;

    /// <summary>
    /// Move to an existing film (RPP #1522): the episode is another copy of a film already in the catalogue, so its
    /// links go onto that film and the episode goes away. Every failure stops before the episode row is deleted:
    /// <list type="number">
    /// <item>Materialise the episode's Spotify / Apple / YouTube ids into <c>services</c> urls, as a move does.</item>
    /// <item>Plan the link merge (<see cref="ServiceLinkMerge"/>): links for services the film has none for are added;
    /// identical links are already held; a different YouTube upload is added as an <c>additionalYouTube</c> entry
    /// (RPP #1411) so merging Manic-style re-uploads works; a different link for Spotify, Apple or another
    /// single-valued service is a 409 naming the film, and nothing is written.</item>
    /// <item>Save the film with the added links, any YouTube alternates, and the episode's subjects (not ones the film
    /// removed), ETag-guarded and re-read on a conflict.</item>
    /// <item>Push the film's search document, then delete the episode's. On failure the episode row is kept and a
    /// re-send resumes (the links are then already held).</item>
    /// <item>Re-read the film: gone, or no longer holding the episode's links, stops with the episode row kept.</item>
    /// <item>Delete the episode row and point its short link at the film page (best effort).</item>
    /// </list>
    /// The film then owns the episode's urls, so the Indexer's catalogue-owned guard (#1068) does not re-add a recently
    /// released episode to the podcast. The film's name, identity, release and description are not changed. A re-send
    /// after the episode row is gone is a 404 (the episode id is not kept anywhere). The source podcast is not changed.
    /// </summary>
    private async Task<EpisodeFilmMoveResult> MergeIntoFilmAsync(
        Podcast podcast,
        Episode? episode,
        Guid filmId,
        CancellationToken cancellationToken)
    {
        if (episode is null)
        {
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound, Error: "Episode not found");
        }

        if (episode.Removed)
        {
            return new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.BadRequest,
                Error: "A removed episode cannot be moved to a film");
        }

        // #1484 / #1416: a split-off stays hidden until a curator approves it.
        if (episode.IsHeldForApproval())
        {
            return new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.BadRequest,
                Error: "An episode held for approval cannot be moved to a film");
        }

        if (filmId == episode.Id)
        {
            return new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.BadRequest,
                Error: "This film was made from this episode; send Move to film without filmId to finish that move");
        }

        await episodeServiceMaterialiser.MaterialiseAsync(podcast, episode, cancellationToken);
        var source = ServiceLinkMap.CloneWithoutNulls(episode.Services) ?? new Dictionary<string, ServiceLink>();
        if (!source.Values.Any(link => link.Url is not null))
        {
            return new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.BadRequest,
                Error: "The episode has no links to add to the film");
        }

        Film? film = null;
        for (var attempt = 1; attempt <= MaxMergeSaveAttempts && film is null; attempt++)
        {
            var version = await filmRepository.GetFilmForUpdate(filmId);
            if (version is null)
            {
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound, Error: "Film not found");
            }

            var latest = version.Film;
            if (latest.IsRemoved())
            {
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.BadRequest, latest.Id, latest.Name,
                    "A removed film cannot take an episode's links");
            }

            var plan = ServiceLinkMerge.Plan(latest.Services, source, latest.AdditionalYouTube);
            if (plan.HasConflicts)
            {
                var conflict = plan.Conflicts[0];
                logger.LogWarning(
                    "{method}: Film '{filmId}' already has a different {service} link; episode '{episodeId}' not merged.",
                    nameof(MergeIntoFilmAsync), latest.Id, conflict.Service, episode.Id);
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.Conflict, latest.Id, latest.Name,
                    $"Film '{latest.Name}' already has a different {conflict.Service} link ({conflict.TargetUrl}). " +
                    "A film holds one link for this service.");
            }

            var addedSubjects = episode.Subjects
                .Where(subject => !latest.Subjects.Contains(subject, StringComparer.OrdinalIgnoreCase) &&
                                  !latest.RemovedSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (!plan.HasWork && addedSubjects.Count == 0)
            {
                film = latest;
                break;
            }

            latest.Services = ServiceLinkMerge.Apply(latest.Services, plan);
            latest.AdditionalYouTube = ServiceLinkMerge.ApplyYouTubeAlternates(
                latest.AdditionalYouTube,
                plan,
                DateTime.UtcNow,
                episode,
                string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) ? null : podcast.YouTubeChannelId,
                podcast.Name);
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(latest);
            latest.Subjects.AddRange(addedSubjects);
            if (await filmRepository.TryReplace(latest, version.ETag))
            {
                var added = plan.ToAdd.Keys.Concat(
                    plan.YouTubeAlternatesToAdd.Count > 0 ? ["youtube-alternate"] : Array.Empty<string>());
                logger.LogInformation(
                    "{method}: Added {services} and {subjects} subject(s) from episode '{episodeId}' to film '{filmId}'.",
                    nameof(MergeIntoFilmAsync), string.Join(", ", added), addedSubjects.Count, episode.Id,
                    latest.Id);
                film = latest;
                break;
            }

            logger.LogWarning(
                "{method}: Film '{filmId}' changed while merging episode '{episodeId}' (attempt {attempt} of {max}); re-reading.",
                nameof(MergeIntoFilmAsync), filmId, episode.Id, attempt, MaxMergeSaveAttempts);
        }

        if (film is null)
        {
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, filmId,
                Error: "The film kept changing; send again");
        }

        if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(film)], cancellationToken))
        {
            logger.LogError(
                "{method}: Search push failed for film '{filmId}'. Episode row kept; re-send to resume.",
                nameof(MergeIntoFilmAsync), film.Id);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
        }

        if (!await searchSwap.DeleteAsync([episode.Id], cancellationToken))
        {
            logger.LogError(
                "{method}: Search delete failed for episode '{episodeId}'. Episode row kept; re-send to resume.",
                nameof(MergeIntoFilmAsync), episode.Id);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
        }

        // Last check before the only irreversible step: a concurrent film write could have dropped the links.
        var current = await filmRepository.GetFilm(film.Id);
        if (current is null || ServiceLinkMerge.Plan(current.Services, source, current.AdditionalYouTube) is
                { HasWork: true } or { HasConflicts: true })
        {
            logger.LogError(
                "{method}: Film '{filmId}' was deleted or lost episode '{episodeId}' links after the save. Episode row kept.",
                nameof(MergeIntoFilmAsync), film.Id, episode.Id);
            await RestoreEpisodeSearchDocument(podcast, episode, cancellationToken);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
        }

        await episodeRepository.Delete(podcast.Id, episode.Id);
        logger.LogInformation(
            "Merged episode '{episodeId}' of podcast '{podcastId}' into existing film '{filmId}'.",
            episode.Id, podcast.Id, film.Id);
        await RedirectShortLink(podcast, episode, film);
        return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, film.Id, film.Name);
    }

    /// <summary>Best effort: put the episode's search document back when the merge stops after deleting it.</summary>
    private async Task RestoreEpisodeSearchDocument(Podcast podcast, Episode episode, CancellationToken cancellationToken)
    {
        try
        {
            if (!await searchSwap.UploadAsync([new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord()],
                    cancellationToken))
            {
                logger.LogError("{method}: Search re-upload failed for episode '{episodeId}'.",
                    nameof(RestoreEpisodeSearchDocument), episode.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Search re-upload failed for episode '{episodeId}'.",
                nameof(RestoreEpisodeSearchDocument), episode.Id);
        }
    }

    /// <summary>
    /// Merge only: the episode id leaves search (the film keeps its own id), so the short-link record is pointed at
    /// the film page rather than deleted, as Attach as trailer does. Best effort.
    /// </summary>
    private async Task RedirectShortLink(Podcast podcast, Episode episode, Film film)
    {
        try
        {
            var path = CatalogueShortId.PlayablePath(film.Name.Trim(), film.Id, CatalogueShortId.Film);
            var result = await shortnerService.Redirect(new PodcastEpisode(podcast, episode), path);
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short link for merged episode '{episodeId}' was not pointed at film '{filmId}'.",
                    nameof(RedirectShortLink), episode.Id, film.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link redirect failed for merged episode '{episodeId}'.",
                nameof(RedirectShortLink), episode.Id);
        }
    }

    /// <summary>
    /// The short-link key is the episode id (base 64), which the film keeps. With no record the Shortner looks the id up
    /// in search (now ContentKind Film) and redirects to <c>/film/{name}/{shortId}</c>. The move has already succeeded,
    /// so a failure is logged and does not fail the request.
    /// </summary>
    private async Task DeleteShortLink(
        RedditPodcastPoster.Models.Podcasts.Podcast podcast,
        RedditPodcastPoster.Models.Episodes.Episode episode)
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
