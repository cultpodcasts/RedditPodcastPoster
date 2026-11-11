using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsOrganisationExtension
{
    public static NewsOrganisationDto ToDto(this NewsOrganisation organisation)
    {
        var dto = new NewsOrganisationDto().WithPublisherFields(organisation).WithServiceIds(organisation.Indexing);
        var channels = organisation.AdditionalYouTubeChannels ?? [];
        dto.AdditionalYouTubeChannels = channels
            .Select(x => new AdditionalYouTubeChannelDto
            {
                YouTubeChannelId = x.YouTubeChannelId,
                YouTubePlaylistId = x.YouTubePlaylistId
            })
            .ToArray();
        dto.AdditionalYouTubeChannelIds = channels.Select(x => x.YouTubeChannelId).ToArray();
        return dto;
    }
}
