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
                Message = x.Message
            }).ToArray(),
            SearchIndexerState = outcome.SearchIndexer.ToDto()
        };
    }
}
