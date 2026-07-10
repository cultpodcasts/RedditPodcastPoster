using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People;

public interface IPersonGuestHandleResolver
{
    Task<(string[] TwitterHandles, string[] BlueskyHandles)> Resolve(Episode episode);
}

public class PersonGuestHandleResolver(IPersonService personService) : IPersonGuestHandleResolver
{
    public async Task<(string[] TwitterHandles, string[] BlueskyHandles)> Resolve(Episode episode)
    {
        var twitterHandles = NormalizeHandleArray(episode.TwitterHandles);
        var blueskyHandles = NormalizeHandleArray(episode.BlueskyHandles);
        if (twitterHandles.Length > 0 || blueskyHandles.Length > 0)
        {
            return (twitterHandles, blueskyHandles);
        }

        if (episode.Guests is not { Length: > 0 })
        {
            return ([], []);
        }

        var people = await personService.GetByNames(episode.Guests);
        twitterHandles = people
            .Select(x => x.TwitterHandle)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
        blueskyHandles = people
            .Select(x => x.BlueskyHandle)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
        return (twitterHandles, blueskyHandles);
    }

    private static string[] NormalizeHandleArray(string[]? handles) =>
        handles?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray() ?? [];
}
