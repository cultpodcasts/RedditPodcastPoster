using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.TvShows;

public class TvShowUpdateService(
    ITvShowRepository tvShowRepository,
    ILogger<TvShowUpdateService> logger) : ITvShowUpdateService
{
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

            await tvShowRepository.Save(show);
            return new TvShowUpdateResult(TvShowUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update TV show '{id}'.", nameof(UpdateAsync), request.TvShowId);
            return new TvShowUpdateResult(TvShowUpdateStatus.Failed);
        }
    }
}
