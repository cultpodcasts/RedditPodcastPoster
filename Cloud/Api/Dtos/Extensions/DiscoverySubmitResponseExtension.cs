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
                : null
        };
    }
}
