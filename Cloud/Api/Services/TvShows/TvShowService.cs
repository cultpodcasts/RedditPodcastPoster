using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.TvShows;

public class TvShowService(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
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

            if (!CanonicalUriPatch.TryApply(request.Change.Imdb, uri => show.Imdb = uri, out var imdbError))
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.BadRequest, imdbError);
            }

            if (!CanonicalUriPatch.TryApply(request.Change.Tvdb, uri => show.Tvdb = uri, out var tvdbError))
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.BadRequest, tvdbError);
            }

            PublisherChangeApplier.Apply(show, request.Change);
            await tvShowRepository.Save(show);

            if (!request.Change.ChangesChildProjection)
            {
                return new TvShowUpdateResult(TvShowUpdateStatus.Accepted);
            }

            // Same as news: episodes carry the show's search terms and language (publisherSearchTerms /
            // publisherLanguage). Saving a changed episode bumps its _ts, so the tvshowepisode indexer picks it up.
            var episodes = await tvShowEpisodeRepository.GetByTvShowId(show.Id).ToListAsync(cancellationToken);
            var changed = episodes.Where(episode => episode.SetTvShowProperties(show).Updated).ToArray();
            if (changed.Length > 0)
            {
                await tvShowEpisodeRepository.Save(changed);
                logger.LogInformation(
                    "{method}: Updated {count} episode(s) of TV show '{id}' with its new properties.",
                    nameof(UpdateAsync), changed.Length, show.Id);
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
