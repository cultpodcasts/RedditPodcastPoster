using Api.Services.YouTube;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.TvShows;

public class TvShowService(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IServiceIdsUniqueness serviceIdsUniqueness,
    IYouTubePlaylistGuard youTubePlaylistGuard,
    IChannelWideYouTubeClaimGuard channelWideYouTubeClaimGuard,
    IIdentityMetadataEnricher identityMetadata,
    ILogger<TvShowService> logger) : ITvShowService
{
    public async Task<TvShowGetResult> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            if (Guid.TryParse(identifier, out var id))
            {
                var byId = await tvShowRepository.GetTvShow(id);
                return byId is null
                    ? new TvShowGetResult(TvShowGetStatus.NotFound)
                    : new TvShowGetResult(TvShowGetStatus.Found, byId);
            }

            var name = PodcastRouteNameNormalizer.Normalize(identifier);
            var matches = await PublisherNameAttachLookup.FindByName(tvShowRepository, name, cancellationToken);
            if (matches.Count == 0)
            {
                return new TvShowGetResult(TvShowGetStatus.NotFound);
            }

            if (matches.Count == 1)
            {
                return new TvShowGetResult(TvShowGetStatus.Found, matches[0]);
            }

            return new TvShowGetResult(
                TvShowGetStatus.Conflict,
                AmbiguousIds: matches.Select(show => show.Id).ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get TV show '{identifier}'.", nameof(GetAsync), identifier);
            return new TvShowGetResult(TvShowGetStatus.Failed);
        }
    }

    public async Task<TvShowUpdateResult> UpdateAsync(
        TvShowChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var show = await tvShowRepository.GetTvShow(request.TvShowId);
            if (show is null)
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.NotFound);
            }

            if (!CanonicalIdChangeApplier.TryValidate(
                    request.Change.ImdbId,
                    request.Change.TmdbId,
                    request.Change.TvdbId,
                    out var idError))
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.BadRequest, idError);
            }

            CanonicalIdChangeApplier.Apply(
                show,
                request.Change.ImdbId,
                request.Change.TmdbId,
                request.Change.TvdbId);
            var playlistCheck = await youTubePlaylistGuard.Check(
                show.Indexing?.YouTubePlaylistId,
                request.Change.YouTubePlaylistId,
                show.Indexing?.YouTubePlaylistOwnerChannelId,
                cancellationToken);
            if (playlistCheck is { IsAllowed: false } refused)
            {
                logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                    show.Id, refused.Code, refused.Message);
                return new TvShowUpdateResult(
                    refused.IsRetryable ? TvShowUpdateStatus.ServiceUnavailable : TvShowUpdateStatus.BadRequest,
                    refused.Message,
                    Code: refused.Code);
            }

            if (ChannelWideYouTubeClaimProjection.ChangeAffectsClaim(request.Change))
            {
                var (projectedChannel, projectedPlaylist, projectedRemoved) =
                    ChannelWideYouTubeClaimProjection.ForParent(
                        show.Indexing, show.IsRemoved(), request.Change);
                if (await channelWideYouTubeClaimGuard.Check(
                        show.Id,
                        ChannelWideYouTubeClaimKind.TvShow,
                        projectedChannel,
                        projectedPlaylist,
                        projectedRemoved,
                        cancellationToken) is { } claimConflict)
                {
                    logger.LogWarning("{method}: '{id}' refused: {code}. {message}", nameof(UpdateAsync),
                        show.Id, ChannelWideYouTubeClaimConflict.Code, claimConflict.Message);
                    return new TvShowUpdateResult(
                        TvShowUpdateStatus.BadRequest,
                        claimConflict.Message,
                        Code: ChannelWideYouTubeClaimConflict.Code);
                }
            }

            PublisherChangeApplier.Apply(show, request.Change);
            await TryFillPublisherAsync(show, request.Change, cancellationToken);
            if (await serviceIdsUniqueness.FindConflict(show.Id, request.Change) is { } conflict)
            {
                logger.LogWarning("{method}: {message}", nameof(UpdateAsync), conflict.Message);
                return new TvShowUpdateResult(TvShowUpdateStatus.Conflict, conflict.Message, conflict.OwnerIds);
            }

            show.Indexing = ServiceIdsApplier.Apply(show.Indexing, request.Change, show, logger);
            if (YouTubePlaylistOwnerChannel.DerivedOwnerIfChannelBlank(
                    playlistCheck, show.Indexing?.YouTubeChannelId) is { } derivedOwner)
            {
                // Derive runs after Apply, so request.Change never included this channel for FindConflict.
                // On conflict, skip derive + warn and still save the playlist — playlist-only edits stay
                // allowed; do not refuse the whole PATCH for an automatic channel write.
                if (await serviceIdsUniqueness.FindConflict(
                        show.Id,
                        new TvShowChangeRequest { YouTubeChannelId = derivedOwner }) is { } deriveConflict)
                {
                    logger.LogWarning(
                        "{method}: skipping derived youTubeChannelId '{channelId}' for '{id}': {message}",
                        nameof(UpdateAsync), derivedOwner, show.Id, deriveConflict.Message);
                }
                else
                {
                    show.Indexing ??= new CatalogueIndexingSource();
                    show.Indexing.YouTubeChannelId = derivedOwner;
                }
            }

            YouTubePlaylistOwnerChannel.PersistFromCheck(
                show.Indexing?.YouTubePlaylistId,
                playlistCheck,
                () =>
                {
                    if (show.Indexing != null)
                    {
                        show.Indexing.YouTubePlaylistOwnerChannelId = null;
                    }
                },
                owner =>
                {
                    show.Indexing ??= new CatalogueIndexingSource();
                    show.Indexing.YouTubePlaylistOwnerChannelId = owner;
                });

            await tvShowRepository.Save(show);

            if (!request.Change.ChangesChildProjection)
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.Accepted);
            }

            // Same as news: episodes carry the show's search terms, language and removed; removal cascades like a
            // podcast removal (see ParentChangeCascade).
            var cascade = await ParentChangeCascade.Run(
                tvShowEpisodeRepository.GetByTvShowId(show.Id),
                episode => episode.SetTvShowProperties(show).Updated,
                tvShowEpisodeRepository.Save,
                request.Change.Removed == true,
                show.Name,
                searchIndexCleanup,
                logger,
                cancellationToken);

            // The cascade is idempotent: a 500 here tells the curator to retry, which finishes the search clean-up.
            if (!cascade.SearchDeleteSucceeded)
            {
                logger.LogError("{method}: removed '{id}' but deleting its children's search documents failed.",
                    nameof(UpdateAsync), show.Id);
                return new TvShowUpdateResult(TvShowUpdateStatus.Failed, "Search clean-up failed; retry the request.");
            }

            return new TvShowUpdateResult(TvShowUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update TV show '{id}'.", nameof(UpdateAsync), request.TvShowId);
            return new TvShowUpdateResult(TvShowUpdateStatus.Failed);
        }
    }

    /// <summary>
    /// RPP #1436: fill an empty publisher from TMDB (first network, else first production company) when the show has
    /// an IMDb or TMDB id and this PATCH did not set or clear publisher.
    /// </summary>
    private async Task TryFillPublisherAsync(
        RedditPodcastPoster.Models.TvShows.TvShow show,
        TvShowChangeRequest change,
        CancellationToken cancellationToken)
    {
        var skip = new IdentityFieldSkip(Publisher: change.PublisherName is not null);
        if (!IdentityMetadataFill.HasGaps(show, skip))
        {
            return;
        }

        var imdbId = change.ImdbId is null
            ? show.ImdbId
            : change.ImdbId.Trim().Length == 0 ? null : change.ImdbId.Trim();
        var tmdbId = change.TmdbId is null
            ? show.TmdbId
            : change.TmdbId.Value == 0 ? null : change.TmdbId;
        if (imdbId is null && tmdbId is null)
        {
            return;
        }

        var metadata = await identityMetadata.FetchTvShowAsync(tmdbId, imdbId, cancellationToken);
        if (metadata is not null)
        {
            IdentityMetadataFill.Fill(show, metadata, skip);
        }
    }
}
