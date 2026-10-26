using Api.Models;
using RedditPodcastPoster.Catalogue.Authority;

namespace Api.Dtos.Extensions;

public static class EpisodeFilmMoveExtensions
{
    public static Models.EpisodeFilmMoveRequest ToModel(this EpisodeFilmMoveRequest? request) => new()
    {
        Title = request?.Title,
        IdentityDecision = request?.IdentityDecision,
        SelectedIdentityKey = request?.SelectedIdentityKey
    };

    public static EpisodeFilmMoveResponse ToDto(this EpisodeFilmMoveResult result) =>
        new()
        {
            FilmId = result.FilmId,
            Name = result.Name,
            Error = result.Error,
            FailureIndexing = result.FailureIndexing,
            IdentityDecision = result.IdentityHold is { } hold ? ToIdentityDecision(hold) : null,
            Cancelled = result.Status == EpisodeFilmMoveStatus.Cancelled
        };

    private static SubmitUrlResponse.IdentityDecisionBody ToIdentityDecision(CatalogueIdentityHold hold) =>
        new()
        {
            Target = hold.Target,
            Query = hold.Query,
            Candidates = hold.Candidates.Select(x => new SubmitUrlResponse.IdentityCandidateBody
            {
                Key = x.Key,
                Title = x.Title,
                Year = x.Year,
                Kind = x.Kind,
                Source = x.Source,
                TmdbId = x.TmdbId,
                TvdbId = x.TvdbId,
                PageUrl = x.PageUrl?.ToString(),
                ThumbnailUrl = x.ThumbnailUrl?.ToString(),
                Description = x.Description
            }).ToArray()
        };
}
