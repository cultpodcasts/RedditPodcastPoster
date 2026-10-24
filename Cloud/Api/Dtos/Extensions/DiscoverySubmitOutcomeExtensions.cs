using Api.Models;

namespace Api.Dtos.Extensions;

public static class DiscoverySubmitOutcomeExtensions
{
    public static DiscoverySubmitOutcomeItemDto ToDto(this DiscoverySubmitOutcomeItem item) => new()
    {
        DiscoveryItemId = item.DiscoveryItemId,
        State = item.State,
        PodcastId = item.PodcastId,
        EpisodeId = item.EpisodeId,
        Message = item.Message,
        EpisodeName = item.EpisodeName,
        ShowName = item.ShowName
    };
}
