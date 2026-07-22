
namespace Api.Dtos.Extensions;

public static class SubjectExtension
{
    public static SubjectDto ToDto(this RedditPodcastPoster.Models.Subjects.Subject subject)
    {
        return new SubjectDto
        {
            Id = subject.Id,
            Aliases = subject.Aliases,
            AssociatedSubjects = subject.AssociatedSubjects,
            Name = subject.Name,
            EnrichmentHashTags = subject.EnrichmentHashTags,
            HashTag = subject.HashTag,
            RedditFlairTemplateId = subject.RedditFlairTemplateId,
            RedditFlareText = subject.RedditFlareText,
            SubjectType = subject.SubjectType,
            KnownTerms = subject.KnownTerms
        };
    }
}
