namespace RedditPodcastPoster.Subjects;

public record SubjectEnrichmentOptions(
    string[]? IgnoredAssociatedSubjects = null,
    string[]? IgnoredSubjects = null,
    string? DefaultSubject = null);