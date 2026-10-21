using RedditPodcastPoster.Models.News;

namespace Api.Dtos.Extensions;

public static class NewsOrganisationExtension
{
    public static NewsOrganisationDto ToDto(this NewsOrganisation organisation)
    {
        return new NewsOrganisationDto
        {
            Id = organisation.Id,
            Name = organisation.Name,
            Language = organisation.Language,
            Removed = organisation.IsRemoved(),
            TwitterHandle = organisation.TwitterHandle,
            BlueskyHandle = organisation.BlueskyHandle,
            HashTag = organisation.HashTag,
            EnrichmentHashTags = organisation.EnrichmentHashTags,
            SearchTerms = organisation.SearchTerms,
            KnownTerms = organisation.KnownTerms,
            DefaultSubject = organisation.DefaultSubject,
            IgnoredSubjects = organisation.IgnoredSubjects,
            IgnoredAssociatedSubjects = organisation.IgnoredAssociatedSubjects,
            LatestReleased = organisation.LatestReleased,
            LastIndexed = organisation.LastIndexed
        };
    }
}
