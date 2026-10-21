using RedditPodcastPoster.Models.News;

namespace Api.Models;

public enum NewsOrganisationGetStatus
{
    Found,
    NotFound,
    Conflict,
    Failed
}

public record NewsOrganisationGetResult(
    NewsOrganisationGetStatus Status,
    NewsOrganisation? NewsOrganisation = null,
    IReadOnlyList<Guid>? AmbiguousIds = null);
