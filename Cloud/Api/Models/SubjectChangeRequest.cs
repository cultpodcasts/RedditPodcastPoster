using RedditPodcastPoster.Models.Subjects;

namespace Api.Models;

public class SubjectChangeRequest
{
    public Guid? Id { get; set; }

    public string[]? Aliases { get; set; }

    public string[]? AssociatedSubjects { get; set; }

    public string? Name { get; set; }

    public string[]? EnrichmentHashTags { get; set; }

    public string? HashTag { get; set; }

    public Guid? RedditFlairTemplateId { get; set; }

    public string? RedditFlareText { get; set; }

    public SubjectType? SubjectType { get; set; }

    public string[]? KnownTerms { get; set; }
}
