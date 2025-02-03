namespace RedditPodcastPoster.Subjects.Models;

public record SubjectEnrichmentOptions(
    string[]? IgnoredAssociatedSubjects,
    string[]? IgnoredSubjects,
    string? DefaultSubject,
    string DescriptionRegex);