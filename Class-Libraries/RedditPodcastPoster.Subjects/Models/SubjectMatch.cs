using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects.Models;

public record SubjectMatch(Subject Subject, MatchResult[] MatchResults);