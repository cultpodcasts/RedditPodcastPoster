using Api.Services.YouTube;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news organisation. Mirrors <c>TvShowService</c> for lookup (id, or a unique name).
/// The editable set is the shared publisher fields, including publisher, description and removed (which cascades to
/// the reports). Name is not part of PATCH: it is changed by <c>POST newsorganisation/name/{identifier}</c> (<c>CatalogueRenameService</c>), which records a redirect.
/// A news organisation may also hold <c>additionalYouTubeChannels</c> (#1370, <see cref="AdditionalYouTubeChannels"/>),
/// each with an optional playlist on that channel: each new playlist is validated against its channel, and each channel
/// passes the same channel-wide claim guard (when no playlist scopes it) and uniqueness check as the main channel before
/// anything is saved.
/// </summary>
public class NewsOrganisationService(
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IServiceIdsUniqueness serviceIdsUniqueness,
    IYouTubePlaylistGuard youTubePlaylistGuard,
    IChannelWideYouTubeClaimGuard channelWideYouTubeClaimGuard,
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

            if (AdditionalYouTubeChannels.Check(organisation.Indexing, request.Change) is { } channelsRefused)
            {
                logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                    organisation.Id, channelsRefused.Code, channelsRefused.Message);
                return new NewsOrganisationUpdateResult(
                    NewsOrganisationUpdateStatus.BadRequest, channelsRefused.Message, Code: channelsRefused.Code);
            }

            var playlistCheck = await youTubePlaylistGuard.Check(
                organisation.Indexing?.YouTubePlaylistId,
                request.Change.YouTubePlaylistId,
                organisation.Indexing?.YouTubePlaylistOwnerChannelId,
                cancellationToken);
            if (playlistCheck is { IsAllowed: false } refused)
            {
                logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                    organisation.Id, refused.Code, refused.Message);
                return new NewsOrganisationUpdateResult(
                    refused.IsRetryable ? NewsOrganisationUpdateStatus.ServiceUnavailable : NewsOrganisationUpdateStatus.BadRequest,
                    refused.Message,
                    Code: refused.Code);
            }

            if (await AdditionalYouTubeChannels.CheckPlaylists(
                    organisation.Indexing, request.Change, youTubePlaylistGuard, cancellationToken) is
                { } additionalPlaylistRefused)
            {
                logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                    organisation.Id, additionalPlaylistRefused.Code, additionalPlaylistRefused.Message);
                return new NewsOrganisationUpdateResult(
                    additionalPlaylistRefused.IsRetryable
                        ? NewsOrganisationUpdateStatus.ServiceUnavailable
                        : NewsOrganisationUpdateStatus.BadRequest,
                    additionalPlaylistRefused.Message,
                    Code: additionalPlaylistRefused.Code);
            }

            if (ChannelWideYouTubeClaimProjection.ChangeAffectsClaim(request.Change))
            {
                var (projectedChannel, projectedPlaylist, projectedRemoved) =
                    ChannelWideYouTubeClaimProjection.ForParent(
                        organisation.Indexing, organisation.IsRemoved(), request.Change);
                if (await channelWideYouTubeClaimGuard.Check(
                        organisation.Id,
                        ChannelWideYouTubeClaimKind.NewsOrganisation,
                        projectedChannel,
                        projectedPlaylist,
                        projectedRemoved,
                        cancellationToken) is { } claimConflict)
                {
                    logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                        organisation.Id, ChannelWideYouTubeClaimConflict.Code, claimConflict.Message);
                    return new NewsOrganisationUpdateResult(
                        NewsOrganisationUpdateStatus.BadRequest,
                        claimConflict.Message,
                        Code: ChannelWideYouTubeClaimConflict.Code);
                }
            }

            if (AdditionalYouTubeChannels.ListRequested(request.Change) || request.Change.Removed != null)
            {
                // An additional channel is claimed channel-wide unless its own playlist scopes it.
                var projectedRemoved = request.Change.Removed ?? organisation.IsRemoved();
                foreach (var additionalChannel in AdditionalYouTubeChannels.Projected(organisation.Indexing, request.Change))
                {
                    if (await channelWideYouTubeClaimGuard.Check(
                            organisation.Id,
                            ChannelWideYouTubeClaimKind.NewsOrganisation,
                            additionalChannel.YouTubeChannelId,
                            additionalChannel.YouTubePlaylistId,
                            projectedRemoved,
                            cancellationToken) is { } additionalConflict)
                    {
                        logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                            organisation.Id, ChannelWideYouTubeClaimConflict.Code, additionalConflict.Message);
                        return new NewsOrganisationUpdateResult(
                            NewsOrganisationUpdateStatus.BadRequest,
                            additionalConflict.Message,
                            Code: ChannelWideYouTubeClaimConflict.Code);
                    }
                }
            }

            PublisherChangeApplier.Apply(organisation, request.Change);
            if (await serviceIdsUniqueness.FindConflict(organisation.Id, request.Change) is { } conflict)
            {
                logger.LogWarning("{method}: {message}", nameof(UpdateAsync), conflict.Message);
                return new NewsOrganisationUpdateResult(NewsOrganisationUpdateStatus.Conflict, conflict.Message, conflict.OwnerIds);
            }

            organisation.Indexing = ServiceIdsApplier.Apply(organisation.Indexing, request.Change, organisation, logger);
            organisation.Indexing = AdditionalYouTubeChannels.Apply(organisation.Indexing, request.Change);
            if (YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(
                    playlistCheck, organisation.Indexing?.YouTubeChannelId) is { } derivedOwner &&
                organisation.Indexing?.HoldsAdditionalYouTubeChannel(derivedOwner) != true)
            {
                // Derive runs after Apply, so request.Change never included this channel for FindConflict.
                // On conflict, skip derive + warn and still save the playlist — playlist-only edits stay
                // allowed; do not refuse the whole PATCH for an automatic channel write.
                if (await serviceIdsUniqueness.FindConflict(
                        organisation.Id,
                        new NewsOrganisationChangeRequest { YouTubeChannelId = derivedOwner }) is { } deriveConflict)
                {
                    logger.LogWarning(
                        "{method}: skipping derived youTubeChannelId '{channelId}' for '{id}': {message}",
                        nameof(UpdateAsync), derivedOwner, organisation.Id, deriveConflict.Message);
                }
                else
                {
                    organisation.Indexing ??= new CatalogueIndexingSource();
                    organisation.Indexing.YouTubeChannelId = derivedOwner;
                }
            }

            YouTubePlaylistOwnerChannel.PersistFromCheck(
                organisation.Indexing?.YouTubePlaylistId,
                playlistCheck,
                () =>
                {
                    if (organisation.Indexing != null)
                    {
                        organisation.Indexing.YouTubePlaylistOwnerChannelId = null;
                    }
                },
                owner =>
                {
                    organisation.Indexing ??= new CatalogueIndexingSource();
                    organisation.Indexing.YouTubePlaylistOwnerChannelId = owner;
                });

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
