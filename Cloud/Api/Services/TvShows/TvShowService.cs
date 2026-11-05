using Api.Services.YouTube;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
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

            PublisherChangeApplier.Apply(show, request.Change);
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
}
