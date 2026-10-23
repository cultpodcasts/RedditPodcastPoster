using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Catalogue;

/// <summary>
/// Renames a news organisation, TV show or film the way <c>PodcastRenameService</c> renames a podcast:
/// <list type="number">
/// <item>Reject a new name containing <c>/</c> (InvalidName) and a clash with another entity of the same kind,
/// compared case-insensitively (Conflict).</item>
/// <item>Record the redirect in the Cloudflare KV redirect namespace (<see cref="IRedirectService"/>), with the same
/// loop guard. If the guard refuses, the result is BadRequest and nothing is changed.</item>
/// <item>Copy the new name onto the denormalised children (<c>newsOrganisationName</c> / <c>tvShowName</c>), as
/// <c>HydrateDetachedEpisodePodcastProjection</c> copies <c>podcastName</c>.</item>
/// <item>Update search straight away (<see cref="PlayableSearchRename"/>), as the podcast rename runs
/// <c>IndexEpisodes</c>.</item>
/// <item>Leave the stored <c>fileKey</c> unchanged, as a podcast rename does.</item>
/// </list>
/// The differences are for safety. The entity is addressed by guid or by a unique name (one entity, so there is no
/// TooMany). Children are saved before the parent, and only the ones that changed, so a failed run can be retried with
/// the same request. A retry after the parent was saved finds the new name already in place: it skips the redirect,
/// repairs any stale children and re-runs the search update. The redirect is not written while another entity of the
/// same kind still has the old name, because the old URL still belongs to that entity.
/// </summary>
public class CatalogueRenameService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IRedirectService redirectService,
    PlayableSearchRename searchRename,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    ILogger<CatalogueRenameService> logger) : ICatalogueRenameService
{
    public Task<CatalogueRenameResult> RenameNewsOrganisation(CatalogueRenameCommand command, CancellationToken c) =>
        Rename(
            CatalogueRedirectKind.NewsOrganisation,
            newsOrganisationRepository,
            newsOrganisationRepository.GetNewsOrganisation,
            command,
            async (organisation, ct) => (await ParentChangeCascade.Run(
                newsReportRepository.GetByNewsOrganisationId(organisation.Id),
                report => report.SetNewsOrganisationProperties(organisation).Updated,
                newsReportRepository.Save,
                parentRemovedRequested: false,
                organisation.Name,
                searchIndexCleanup,
                logger,
                ct)).ChildIds,
            newsOrganisationRepository.Save,
            PlayableSearchRename.SeriesNameField,
            c);

    public Task<CatalogueRenameResult> RenameTvShow(CatalogueRenameCommand command, CancellationToken c) =>
        Rename(
            CatalogueRedirectKind.TvShow,
            tvShowRepository,
            tvShowRepository.GetTvShow,
            command,
            async (show, ct) => (await ParentChangeCascade.Run(
                tvShowEpisodeRepository.GetByTvShowId(show.Id),
                episode => episode.SetTvShowProperties(show).Updated,
                tvShowEpisodeRepository.Save,
                parentRemovedRequested: false,
                show.Name,
                searchIndexCleanup,
                logger,
                ct)).ChildIds,
            tvShowRepository.Save,
            PlayableSearchRename.SeriesNameField,
            c);

    public Task<CatalogueRenameResult> RenameFilm(CatalogueRenameCommand command, CancellationToken c) =>
        Rename(
            CatalogueRedirectKind.Film,
            filmRepository,
            filmRepository.GetFilm,
            command,
            (film, _) => Task.FromResult<IReadOnlyList<Guid>>([film.Id]),
            SaveRenamedFilm,
            PlayableSearchRename.TitleField,
            c);

    /// <summary>
    /// The name is copied onto a freshly read film. A trailer attached after the rename loaded the film stays,
    /// because the replace is ETag-guarded and retried on 412.
    /// </summary>
    private async Task SaveRenamedFilm(Film film)
    {
        var name = film.Name;
        if (await filmRepository.Update(film.Id, latest =>
            {
                latest.Name = name;
                return true;
            }) is null)
        {
            throw new InvalidOperationException($"Film '{film.Id}' was deleted during the rename.");
        }
    }

    private async Task<CatalogueRenameResult> Rename<TEntity>(
        CatalogueRedirectKind kind,
        IFilterableRepository<TEntity> repository,
        Func<Guid, Task<TEntity?>> getById,
        CatalogueRenameCommand command,
        Func<TEntity, CancellationToken, Task<IReadOnlyList<Guid>>> propagate,
        Func<TEntity, Task> save,
        string searchField,
        CancellationToken c) where TEntity : Publisher
    {
        try
        {
            var newName = command.NewName?.Trim() ?? "";
            if (newName.Length == 0)
            {
                return new CatalogueRenameResult(CatalogueRenameStatus.BadRequest);
            }

            if (newName.Contains('/'))
            {
                logger.LogError("New {kind} name contains invalid-character: '{NewName}'.", kind, newName);
                return new CatalogueRenameResult(CatalogueRenameStatus.InvalidName);
            }

            TEntity? entity;
            if (Guid.TryParse(command.Identifier, out var id))
            {
                entity = await getById(id);
            }
            else
            {
                var matches = await PublisherNameAttachLookup.FindByName(
                    repository, PodcastRouteNameNormalizer.Normalize(command.Identifier), c);
                if (matches.Count > 1)
                {
                    return new CatalogueRenameResult(CatalogueRenameStatus.Conflict,
                        AmbiguousIds: matches.Select(x => x.Id).ToArray());
                }

                entity = matches.SingleOrDefault();
            }

            if (entity is null)
            {
                logger.LogError("{kind} not found with identifier '{identifier}'.", kind, command.Identifier);
                return new CatalogueRenameResult(CatalogueRenameStatus.NotFound);
            }

            var oldName = entity.Name;
            CatalogueRedirect? redirect = null;
            var alreadyRenamed = oldName == newName;
            if (!alreadyRenamed)
            {
                var clashes = await PublisherNameAttachLookup.FindByName(repository, newName, c);
                if (clashes.Any(x => x.Id != entity.Id))
                {
                    logger.LogError("{kind} found with new-name '{newName}'.", kind, newName);
                    return new CatalogueRenameResult(CatalogueRenameStatus.Conflict);
                }

                var homonyms = await PublisherNameAttachLookup.FindByName(repository, oldName, c);
                if (homonyms.Any(x => x.Id != entity.Id))
                {
                    logger.LogWarning(
                        "Not redirecting {kind} '{oldName}' -> '{newName}': another {kind} still has the old name.",
                        kind, oldName, newName, kind);
                }
                else
                {
                    redirect = new CatalogueRedirect(kind, oldName, newName);
                    if (!await redirectService.IsCatalogueRedirectSafe(redirect))
                    {
                        return new CatalogueRenameResult(CatalogueRenameStatus.BadRequest);
                    }
                }

                entity.Name = newName;
            }

            // Children, then the redirect, then the parent. If the children fail, no redirect is live and the old URL
            // still works; if the redirect fails, the parent is unsaved. Either way a retry of the same request
            // finishes the rename.
            var playableIds = await propagate(entity, c);
            if (!alreadyRenamed)
            {
                if (redirect != null && !await redirectService.CreateCatalogueRedirect(redirect))
                {
                    return new CatalogueRenameResult(CatalogueRenameStatus.BadRequest);
                }

                await save(entity);
                logger.LogInformation("Renamed {kind} '{oldName}' to '{newName}'.", kind, oldName, newName);
            }

            var indexed = await searchRename.SetField(searchField, newName, playableIds, c);
            logger.LogInformation("Search-index run-state: {indexState}.", indexed);
            return new CatalogueRenameResult(CatalogueRenameStatus.Ok, indexed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to rename {kind} '{identifier}'.", nameof(Rename), kind,
                command.Identifier);
            return new CatalogueRenameResult(CatalogueRenameStatus.Failed);
        }
    }
}
