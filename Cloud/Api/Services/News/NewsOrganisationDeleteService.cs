using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.News;

/// <summary>
/// Retires an empty news organisation by deleting its document (#1397), for example the source of a report move
/// (<see cref="NewsReportOrganisationMoveService"/>). Soft removal (<c>PATCH {removed: true}</c>) keeps the document,
/// so its name still makes curator name routes ambiguous (409) when the surviving organisation has the same name; a
/// delete is the only way to clear that.
/// <list type="bullet">
/// <item>Refused (nothing written) while the organisation has any report, removed ones included, so no report is ever
/// orphaned. The count and the delete run under the parent indexing lease (<see cref="IParentIndexingLockStore"/>),
/// so an indexer pass cannot add a report in between; while the organisation is being indexed the delete is refused
/// (409).</item>
/// <item>With <c>redirectToNewsOrganisationId</c> (the organisation its reports went to), when the names differ the old
/// name route is redirected to the target's, with the same Cloudflare KV redirect and loop guard as the admin rename
/// (<see cref="IRedirectService"/>). As in rename, no redirect is written while another news organisation still has the
/// old name (the old URL still belongs to it). The redirect is written before the delete, so a failed delete can be
/// retried with the same request; an unsafe redirect refuses the delete (400).</item>
/// <item>The organisation's file-key claim is released after the delete (best effort), so the key can be reused.</item>
/// </list>
/// Admin only (as <c>DELETE newsreport/{id}</c> and rename): a delete cannot be undone. Take a backup of the document
/// first; it is logged in full before it is deleted.
/// </summary>
public class NewsOrganisationDeleteService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFileKeyAllocator fileKeyAllocator,
    IRedirectService redirectService,
    IParentIndexingLockStore indexingLockStore,
    TimeProvider timeProvider,
    ILogger<NewsOrganisationDeleteService> logger) : INewsOrganisationDeleteService
{
    public const string LockSource = "NewsOrganisationDelete";

    public async Task<NewsOrganisationDeleteResult> DeleteAsync(
        Guid newsOrganisationId,
        Guid? redirectToNewsOrganisationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(newsOrganisationId);
            if (organisation is null)
            {
                return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.NotFound);
            }

            CatalogueRedirect? redirect = null;
            if (redirectToNewsOrganisationId is { } redirectToId)
            {
                if (redirectToId == newsOrganisationId)
                {
                    return BadRequest("redirectToNewsOrganisationId is the organisation being deleted");
                }

                var redirectTo = await newsOrganisationRepository.GetNewsOrganisation(redirectToId);
                if (redirectTo is null)
                {
                    return BadRequest("The redirectToNewsOrganisationId news organisation was not found");
                }

                if (redirectTo.Removed == true)
                {
                    return BadRequest("Cannot redirect to a removed news organisation");
                }

                var oldName = organisation.Name.Trim();
                var newName = redirectTo.Name.Trim();
                if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "{method}: News organisation '{organisationId}' has the same name as '{redirectToId}'; no redirect needed.",
                        nameof(DeleteAsync), newsOrganisationId, redirectToId);
                }
                else
                {
                    var homonyms = await PublisherNameAttachLookup.FindByName(
                        newsOrganisationRepository, oldName, cancellationToken);
                    if (homonyms.Any(item => item.Id != newsOrganisationId))
                    {
                        logger.LogWarning(
                            "{method}: Not redirecting news organisation '{oldName}' -> '{newName}': another news organisation still has the old name.",
                            nameof(DeleteAsync), oldName, newName);
                    }
                    else
                    {
                        redirect = new CatalogueRedirect(CatalogueRedirectKind.NewsOrganisation, oldName, newName);
                        if (!await redirectService.IsCatalogueRedirectSafe(redirect))
                        {
                            return BadRequest(
                                $"A redirect from '{oldName}' to '{newName}' would loop. Nothing was deleted");
                        }
                    }
                }
            }

            await using var lease = await ParentIndexingLease.TryStart(
                indexingLockStore,
                timeProvider,
                newsOrganisationId,
                CatalogueParentsUpdater.IndexingLockStaleAfter,
                NewsReportOrganisationMoveService.LockHeartbeatInterval,
                LockSource,
                logger);
            if (lease is null)
            {
                logger.LogWarning("{method}: News organisation '{organisationId}' is being indexed; not deleted.",
                    nameof(DeleteAsync), newsOrganisationId);
                return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Busy);
            }

            var remaining = await newsReportRepository.Count(newsOrganisationId);
            if (remaining > 0)
            {
                logger.LogWarning(
                    "{method}: News organisation '{organisationId}' still has {count} report(s); not deleted.",
                    nameof(DeleteAsync), newsOrganisationId, remaining);
                return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.HasReports, remaining);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (redirect is not null && !await redirectService.CreateCatalogueRedirect(redirect))
            {
                return BadRequest($"The redirect from '{redirect.OldName}' to '{redirect.NewName}' could not be recorded. Nothing was deleted");
            }

            logger.LogWarning(
                "{method}: Deleting empty news organisation '{name}' ('{organisationId}', file key '{fileKey}', youtube-channel '{channelId}', removed '{removed}', redirect-to '{redirectTo}').",
                nameof(DeleteAsync), organisation.Name, organisation.Id, organisation.FileKey,
                organisation.Indexing?.YouTubeChannelId, organisation.Removed, redirect?.NewName);
            await newsOrganisationRepository.Delete(newsOrganisationId);
            await ReleaseFileKey(organisation.FileKey, organisation.Id);
            return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Deleted)
            {
                RedirectedToName = redirect?.NewName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to delete news organisation '{organisationId}'.",
                nameof(DeleteAsync), newsOrganisationId);
            return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Failed);
        }
    }

    private static NewsOrganisationDeleteResult BadRequest(string error) =>
        new(NewsOrganisationDeleteStatus.BadRequest) { Error = error };

    private async Task ReleaseFileKey(string? fileKey, Guid organisationId)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
        {
            return;
        }

        try
        {
            await fileKeyAllocator.Release(fileKey, organisationId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "{method}: File-key claim '{fileKey}' of deleted news organisation '{organisationId}' was not released.",
                nameof(ReleaseFileKey), fileKey, organisationId);
        }
    }
}
