using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.TvShows;

public class TvShowGetService(
    ITvShowRepository tvShowRepository,
    ILogger<TvShowGetService> logger) : ITvShowGetService
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
}
