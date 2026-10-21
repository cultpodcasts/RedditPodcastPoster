using System.Globalization;
using Api.Dtos.Mapping;
using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsReportExtension
{
    public static NewsReportDto ToDto(this NewsReport report)
    {
        return new NewsReportDto
        {
            Id = report.Id,
            NewsOrganisationId = report.NewsOrganisationId,
            NewsOrganisationName = report.NewsOrganisationName,
            Title = report.Title,
            Description = report.Description,
            Release = report.Release is null
                ? null
                : DateOnly.FromDateTime(report.ReleaseSort).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Duration = report.Length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture),
            Explicit = report.Explicit,
            Ignored = report.Ignored,
            Removed = report.Removed,
            Tweeted = report.Tweeted,
            BlueskyPosted = report.BlueskyPosted,
            Subjects = [.. report.Subjects],
            SearchTerms = report.SearchTerms,
            HashTag = report.HashTag,
            Language = report.Language,
            Guests = report.Guests,
            Services = report.Services.ToDtos()
        };
    }
}
