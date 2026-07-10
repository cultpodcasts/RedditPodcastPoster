using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Models;

namespace RedditPodcastPoster.People;

public interface IPersonService
{
    Task<Person?> Match(string nameOrAlias);
    Task<IEnumerable<PersonMatch>> MatchEpisode(Episode episode, bool withDescription = true);
    Task<IReadOnlyList<Person>> GetByIds(IEnumerable<Guid> ids);
    Task<IReadOnlyList<Person>> GetByNames(IEnumerable<string> names);
}
