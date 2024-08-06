namespace Api.Dtos.Extensions;

public static class SubjectExtension
{
    public static Subject ToDto(this RedditPodcastPoster.Models.Subject subject)
    {
        return new Subject
        {
            Id = subject.Id,
            Aliases = subject.Aliases,
            AssociatedSubjects = subject.AssociatedSubjects,
            Name = subject.Name,
            EnrichmentHashTags = subject.EnrichmentHashTags,
            HashTag = subject.HashTag,
            RedditFlairTemplateId = subject.RedditFlairTemplateId,
            RedditFlareText = subject.RedditFlareText,
            SubjectType = subject.SubjectType
        };
    }
}