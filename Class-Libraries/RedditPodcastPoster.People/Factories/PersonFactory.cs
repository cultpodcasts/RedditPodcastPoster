using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People.Factories;

public interface IPersonFactory
{
    Person Create(string name, string[]? aliases = null, string? twitterHandle = null, string? blueskyHandle = null);
}

public class PersonFactory : IPersonFactory
{
    public Person Create(string name, string[]? aliases = null, string? twitterHandle = null, string? blueskyHandle = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        var person = new Person(name.Trim())
        {
            Aliases = aliases?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray(),
            TwitterHandle = NormalizeHandle(twitterHandle),
            BlueskyHandle = NormalizeHandle(blueskyHandle)
        };
        return person;
    }

    public static string? NormalizeHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim();
        return trimmed.StartsWith('@') ? trimmed : $"@{trimmed}";
    }
}
