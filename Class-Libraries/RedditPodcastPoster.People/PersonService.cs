using System.Net;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.People;

public class PersonService(
    IPersonRepository personRepository,
    ITextSanitiser textSanitiser) : IPersonService
{
    public async Task<Person?> Match(string nameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(nameOrAlias))
        {
            throw new ArgumentNullException(nameof(nameOrAlias));
        }

        var people = await personRepository.GetAll().ToListAsync();
        var term = nameOrAlias.Trim().ToLowerInvariant();

        var matchedPerson = people.SingleOrDefault(x =>
            (!string.IsNullOrEmpty(x.NameKey) && x.NameKey == term) ||
            x.Name.Trim().ToLowerInvariant() == term);
        if (matchedPerson != null)
        {
            return matchedPerson;
        }

        return people.FirstOrDefault(x =>
            x.Aliases != null &&
            x.Aliases.Select(y => y.Trim().ToLowerInvariant()).Contains(term));
    }

    public async Task<IEnumerable<PersonMatch>> MatchEpisode(Episode episode, bool withDescription = true)
    {
        var people = await personRepository.GetAll().ToListAsync();
        return people
            .Select(person => new PersonMatch(
                ToMatchPerson(person),
                MatchTerms(episode, person, withDescription)))
            .Where(x => x.MatchResults.Length > 0)
            .OrderByDescending(x => x.MatchResults.Sum(y => y.Matches))
            .ToArray();
    }

    public async Task<IReadOnlyList<Person>> GetByIds(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        var people = await personRepository.GetAllBy(x => idList.Contains(x.Id)).ToListAsync();
        var byId = people.ToDictionary(x => x.Id);
        return idList.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
    }

    public async Task<IReadOnlyList<Person>> GetByNames(IEnumerable<string> names)
    {
        var nameList = names
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (nameList.Length == 0)
        {
            return [];
        }

        var people = await personRepository.GetAll().ToListAsync();
        var byCanonicalName = people.ToDictionary(
            x => x.Name.Trim(),
            x => x,
            StringComparer.OrdinalIgnoreCase);
        return nameList
            .Where(byCanonicalName.ContainsKey)
            .Select(name => byCanonicalName[name])
            .ToArray();
    }

    private static PersonMatchPerson ToMatchPerson(Person person)
    {
        return new PersonMatchPerson(person.Id, person.Name, person.TwitterHandle, person.BlueskyHandle);
    }

    private PersonMatchResult[] MatchTerms(Episode episode, Person person, bool withDescription)
    {
        var matches = new List<PersonMatchResult>();
        foreach (var term in person.GetNames().Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var matchCount = 0;
            matchCount += CountMatches(term, WebUtility.HtmlDecode(episode.Title));
            if (withDescription)
            {
                var description = textSanitiser.ExtractDescription(episode.Description, string.Empty);
                matchCount += CountMatches(term, WebUtility.HtmlDecode(description));
            }

            if (matchCount > 0)
            {
                matches.Add(new PersonMatchResult(term, matchCount));
            }
        }

        return matches.ToArray();
    }

    private static int CountMatches(string term, string sentence)
    {
        sentence = sentence
            .Replace("’", "'")
            .Replace("´", "'");
        var pattern = @"\b" + Regex.Escape(term) + @"\b";
        var re = new Regex(pattern, RegexOptions.IgnoreCase);
        return re.Matches(sentence).Count;
    }
}
