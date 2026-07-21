using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Models;

public record MatchResult(string Term, int Matches, SubjectMatchSource? Source = null);