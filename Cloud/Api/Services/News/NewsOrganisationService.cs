using Api.Services.YouTube;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news organisation. Mirrors <c>TvShowService</c> for lookup (id, or a unique name).
/// The editable set is the shared publisher fields, including publisher, description and removed (which cascades to
/// the reports). Name is not part of PATCH: it is changed by <c>POST newsorganisation/name/{identifier}</c> (<c>CatalogueRenameService</c>), which records a redirect.
/// </summary>
public class NewsOrganisationService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IServiceIdsUniqueness serviceIdsUniqueness,
    IYouTubePlaylistOwnershipGuard youTubePlaylistOwnershipGuard,
    ILogger<NewsOrganisationService> logger) : INewsOrganisationService
{
    public async Task<NewsOrganisationGetResult> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            if (Guid.TryParse(identifier, out var id))
            {
                var byId = await newsOrganisationRepository.GetNewsOrganisation(id);
                return byId is null
                    ? new NewsOrganisationGetResult(NewsOrganisationGetStatus.NotFound)
                    : new NewsOrganisationGetResult(NewsOrganisationGetStatus.Found, byId);
            }

            var name = PodcastRouteNameNormalizer.Normalize(identifier);
            var matches = await PublisherNameAttachLookup.FindByName(newsOrganisationRepository, name, cancellationToken);
            if (matches.Count == 0)
            {
                return new NewsOrganisationGetResult(NewsOrganisationGetStatus.NotFound);
            }

            if (matches.Count == 1)
            {
                return new NewsOrganisationGetResult(NewsOrganisationGetStatus.Found, matches[0]);
            }

            return new NewsOrganisationGetResult(
                NewsOrganisationGetStatus.Conflict,
                AmbiguousIds: matches.Select(organisation => organisation.Id).ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get news organisation '{identifier}'.", nameof(GetAsync), identifier);
            return new NewsOrganisationGetResult(NewsOrganisationGetStatus.Failed);
        }
    }

    public async Task<NewsOrganisationUpdateResult> UpdateAsync(
        NewsOrganisationChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(request.NewsOrganisationId);
            if (organisation is null)
            {
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.NotFound);
            }

            if (await youTubePlaylistOwnershipGuard.Check(
                    organisation.Indexing?.YouTubeChannelId,
                    organisation.Indexing?.YouTubePlaylistId,
                    request.Change.YouTubeChannelId,
                    request.Change.YouTubePlaylistId,
                    cancellationToken) is { } refused)
            {
                logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                    organisation.Id, refused.Code, refused.Message);
                return new NewsOrganisationUpdateResult(
                    refused.IsRetryable ? NewsOrganisationUpdateStatus.ServiceUnavailable : NewsOrganisationUpdateStatus.BadRequest,
                    refused.Message,
                    Code: refused.Code);
            }

            PublisherChangeApplier.Apply(organisation, request.Change);
            if (await serviceIdsUniqueness.FindConflict(organisation.Id, request.Change) is { } conflict)
            {
                logger.LogWarning("{method}: {message}", nameof(UpdateAsync), conflict.Message);
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Conflict, conflict.Message, conflict.OwnerIds);
            }

            organisation.Indexing = ServiceIdsApplier.Apply(organisation.Indexing, request.Change, organisation, logger);
            await newsOrganisationRepository.Save(organisation);

            if (!request.Change.ChangesChildProjection)
            {
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Accepted);
            }

            // Reports carry the organisation's search terms, language and removed (publisherSearchTerms /
            // publisherLanguage / parentRemoved). Removal cascades like a podcast removal; see ParentChangeCascade.
            var cascade = await ParentChangeCascade.Run(
                newsReportRepository.GetByNewsOrganisationId(organisation.Id),
                report => report.SetNewsOrganisationProperties(organisation).Updated,
                newsReportRepository.Save,
                request.Change.Removed == true,
                organisation.Name,
                searchIndexCleanup,
                logger,
                cancellationToken);

            // The cascade is idempotent: a 500 here tells the curator to retry, which finishes the search clean-up.
            if (!cascade.SearchDeleteSucceeded)
            {
                logger.LogError("{method}: removed '{id}' but deleting its children's search documents failed.",
                    nameof(UpdateAsync), organisation.Id);
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Failed, "Search clean-up failed; retry the request.");
            }

            return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update news organisation '{id}'.", nameof(UpdateAsync),
                request.NewsOrganisationId);
            return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Failed);
        }
    }
}
