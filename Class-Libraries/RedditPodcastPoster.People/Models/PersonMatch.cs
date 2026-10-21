using RedditPodcastPoster.Models.People;

namespace RedditPodcastPoster.People.Models;

public record PersonMatch(Person Person, PersonMatchResult[] MatchResults);

public record PersonMatchResult(string Term, int Matches);
