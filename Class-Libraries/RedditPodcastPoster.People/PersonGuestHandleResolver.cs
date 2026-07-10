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
        var twitterHandles = SocialHandleDeduplicator.Deduplicate(episode.TwitterHandles ?? []);
        var blueskyHandles = SocialHandleDeduplicator.Deduplicate(episode.BlueskyHandles ?? []);
        if (twitterHandles.Length > 0 || blueskyHandles.Length > 0)
        {
            return (twitterHandles, blueskyHandles);
        }

        if (episode.Guests is not { Length: > 0 })
        {
            return ([], []);
        }

        var people = await personService.GetByNames(episode.Guests);
        twitterHandles = SocialHandleDeduplicator.Deduplicate(
            people.Select(x => x.TwitterHandle));
        blueskyHandles = SocialHandleDeduplicator.Deduplicate(
            people.Select(x => x.BlueskyHandle));
        return (twitterHandles, blueskyHandles);
    }
}
