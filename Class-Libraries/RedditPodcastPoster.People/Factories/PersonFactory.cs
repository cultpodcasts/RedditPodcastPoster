using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.People;

namespace RedditPodcastPoster.People.Factories;

public interface IPersonFactory
{
    Person Create(
        string name,
        string[]? aliases = null,
        string? twitterHandle = null,
        string? blueskyHandle = null,
        string? sortName = null,
        bool isOrganization = false);
}

public class PersonFactory : IPersonFactory
{
    public Person Create(
        string name,
        string[]? aliases = null,
        string? twitterHandle = null,
        string? blueskyHandle = null,
        string? sortName = null,
        bool isOrganization = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        var person = new Person(name.Trim())
        {
            IsOrganization = isOrganization,
            SortName = PersonSortNameResolver.ResolveForPersist(name.Trim(), sortName, isOrganization),
            Aliases = aliases?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray(),
            TwitterHandle = NormalizeHandle(twitterHandle),
            BlueskyHandle = NormalizeHandle(blueskyHandle)
        };
        // nameKey is always derived from Name — never from sortName.
        person.EnsureNameKey();
        return person;
    }

    public static string? NormalizeHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var parts = handle
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.StartsWith('@') ? part : $"@{part}")
            .Where(part => part.Length > 1)
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }
}
