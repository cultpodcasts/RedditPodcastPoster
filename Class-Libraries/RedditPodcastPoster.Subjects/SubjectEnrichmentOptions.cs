namespace RedditPodcastPoster.Subjects;

public record SubjectEnrichmentOptions(string[]? IgnoredTerms = null, string? DefaultSubject = null);