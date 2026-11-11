using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsOrganisationExtension
{
    public static NewsOrganisationDto ToDto(this NewsOrganisation organisation)
    {
        var dto = new NewsOrganisationDto().WithPublisherFields(organisation).WithServiceIds(organisation.Indexing);
        dto.AdditionalYouTubeChannelIds = organisation.Indexing?.AdditionalYouTubeChannelIds?.ToArray() ?? [];
        return dto;
    }
}
