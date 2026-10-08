namespace RedditPodcastPoster.People.Models;

public record PersonMatch(PersonMatchPerson Person, PersonMatchResult[] MatchResults);

public record PersonMatchPerson(
    Guid Id,
    string Name,
    string? TwitterHandle,
    string? BlueskyHandle,
    string? SortName = null,
    bool IsOrganization = false,
    string[]? Aliases = null);

public record PersonMatchResult(string Term, int Matches);
