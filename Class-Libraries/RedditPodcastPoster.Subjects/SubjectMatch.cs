using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public record SubjectMatch(Subject Subject, MatchResult[] MatchResults);