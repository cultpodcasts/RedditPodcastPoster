using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Models;

public record SubjectMatch(Subject Subject, MatchResult[] MatchResults);