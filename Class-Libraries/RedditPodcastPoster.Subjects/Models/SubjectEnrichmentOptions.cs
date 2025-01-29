namespace RedditPodcastPoster.Subjects.Models;

public record SubjectEnrichmentOptions(
    string[]? IgnoredAssociatedSubjects = null,
    string[]? IgnoredSubjects = null,
    string? DefaultSubject = null);