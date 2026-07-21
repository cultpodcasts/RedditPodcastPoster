using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;

namespace RedditPodcastPoster.People;

public interface IPersonGuestHandleResolver
{
    Task<(string[] TwitterHandles, string[] BlueskyHandles)> Resolve(Episode episode);
}

/// <summary>
/// Resolves social handles for posting from <see cref="Episode.Guests"/> via the People register.
/// Episode-level handle arrays are retired; guests → Person is the only source.
/// </summary>
public class PersonGuestHandleResolver(IPersonService personService) : IPersonGuestHandleResolver
{
    public async Task<(string[] TwitterHandles, string[] BlueskyHandles)> Resolve(Episode episode)
    {
        if (episode.Guests is not { Length: > 0 })
        {
            return ([], []);
        }

        var people = await personService.GetByNames(episode.Guests);
        var twitterHandles = SocialHandleDeduplicator.Deduplicate(
            people.Select(x => x.TwitterHandle));
        var blueskyHandles = SocialHandleDeduplicator.Deduplicate(
            people.Select(x => x.BlueskyHandle));
        return (twitterHandles, blueskyHandles);
    }
}
