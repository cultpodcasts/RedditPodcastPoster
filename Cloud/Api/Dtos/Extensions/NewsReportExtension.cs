using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsReportExtension
{
    public static NewsReportDto ToDto(this NewsReport report) =>
        new NewsReportDto
        {
            NewsOrganisationId = report.NewsOrganisationId,
            NewsOrganisationName = report.NewsOrganisationName
        }.WithPlayableFields(report);
}
