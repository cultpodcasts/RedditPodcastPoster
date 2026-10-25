using Api.Models;

namespace Api.Dtos.Extensions;

public static class DiscoverySubmitResponseExtension
{
    public static DiscoverySubmitResponse ToDto(this DiscoverySubmitOutcome outcome)
    {
        return new DiscoverySubmitResponse
        {
            Message = outcome.Message,
            ErrorsOccurred = outcome.ErrorsOccurred,
            Results = outcome.Results.Select(x => new DiscoverySubmitResponse.Item
            {
                DiscoveryItemId = x.DiscoveryItemId,
                EpisodeId = x.EpisodeId,
                PodcastId = x.PodcastId,
                Message = x.Message,
                Error = x.Error,
                ContentKind = x.ContentKind
            }).ToArray(),
            SearchIndexerState = outcome.SearchIndexer.ToDto(),
            LanguageDecisionsRequired = outcome.LanguageDecisionsRequired is { Count: > 0 } holds
                ? holds.Select(x => new DiscoverySubmitResponse.LanguageDecision
                {
                    DiscoveryItemId = x.DiscoveryItemId ?? Guid.Empty,
                    Code = x.Code,
                    Name = x.Name,
                    Signals = x.Signals.Select(s => new DiscoverySubmitResponse.LanguageSignal
                    {
                        Field = s.Field,
                        Label = s.Label,
                        Excerpt = s.Excerpt,
                        Reason = s.Reason
                    }).ToArray()
                }).ToArray()
                : null,
            IdentityDecisionsRequired = outcome.IdentityDecisionsRequired is { Count: > 0 } identityHolds
                ? identityHolds.Select(x => new DiscoverySubmitResponse.IdentityDecision
                {
                    DiscoveryItemId = x.DiscoveryItemId ?? Guid.Empty,
                    Target = x.Target,
                    Query = x.Query,
                    Candidates = x.Candidates.Select(c => new DiscoverySubmitResponse.IdentityCandidate
                    {
                        Key = c.Key,
                        Title = c.Title,
                        Year = c.Year,
                        Kind = c.Kind,
                        Source = c.Source,
                        TmdbId = c.TmdbId,
                        TvdbId = c.TvdbId,
                        PageUrl = c.PageUrl?.ToString(),
                        ThumbnailUrl = c.ThumbnailUrl?.ToString(),
                        Description = c.Description
                    }).ToArray()
                }).ToArray()
                : null
        };
    }
}
