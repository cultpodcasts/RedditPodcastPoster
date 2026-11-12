using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.News;

/// <summary>
/// Retires an empty news organisation by deleting its document (#1397), for example the source of a report move
/// (<see cref="NewsReportOrganisationMoveService"/>). Soft removal (<c>PATCH {removed: true}</c>) keeps the document,
/// so its name still makes curator name routes ambiguous (409) when the surviving organisation has the same name; a
/// delete is the only way to clear that.
/// <list type="bullet">
/// <item>Refused (nothing written) while the organisation has any report, removed ones included, so no report is ever
/// orphaned.</item>
/// <item>The organisation's file-key claim is released after the delete (best effort), so the key can be reused.</item>
/// </list>
/// Admin only (as <c>DELETE newsreport/{id}</c> and rename): a delete cannot be undone. Take a backup of the document
/// first; it is logged in full before it is deleted.
/// </summary>
public class NewsOrganisationDeleteService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<NewsOrganisationDeleteService> logger) : INewsOrganisationDeleteService
{
    public async Task<NewsOrganisationDeleteResult> DeleteAsync(
        Guid newsOrganisationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(newsOrganisationId);
            if (organisation is null)
            {
                return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.NotFound);
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
            logger.LogWarning(
                "{method}: Deleting empty news organisation '{name}' ('{organisationId}', file key '{fileKey}', youtube-channel '{channelId}', removed '{removed}').",
                nameof(DeleteAsync), organisation.Name, organisation.Id, organisation.FileKey,
                organisation.Indexing?.YouTubeChannelId, organisation.Removed);
            await newsOrganisationRepository.Delete(newsOrganisationId);
            await ReleaseFileKey(organisation.FileKey, organisation.Id);
            return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to delete news organisation '{organisationId}'.",
                nameof(DeleteAsync), newsOrganisationId);
            return new NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus.Failed);
        }
    }

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
