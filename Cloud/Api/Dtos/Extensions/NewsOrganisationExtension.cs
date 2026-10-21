using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsOrganisationExtension
{
    public static NewsOrganisationDto ToDto(this NewsOrganisation organisation) =>
        new NewsOrganisationDto().WithPublisherFields(organisation);
}
