using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects.Models;

public record MatchResult(string Term, int Matches, SubjectMatchSource? Source = null);