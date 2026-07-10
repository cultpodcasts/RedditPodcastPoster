namespace RedditPodcastPoster.People.Models;

public record PersonMatch(PersonMatchPerson Person, PersonMatchResult[] MatchResults);

public record PersonMatchPerson(Guid Id, string Name, string? TwitterHandle, string? BlueskyHandle);

public record PersonMatchResult(string Term, int Matches);
