using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record SubmitEpisodeDetails(
    bool Spotify,
    bool Apple,
    bool YouTube,
    string[]? Subjects = null,
    PersonMatch[]? People = null,
    PersonMatch[]? GuestSuggestions = null,
    string[]? ExtraServiceKeys = null
)
{
    public static SubmitEpisodeDetails FromEpisode(
        Episode episode,
        string[]? subjects = null,
        PersonMatch[]? people = null,
        PersonMatch[]? guestSuggestions = null) =>
        new(
            EpisodeServicePresence.HasUrl(episode, ServiceKeys.Spotify),
            EpisodeServicePresence.HasUrl(episode, ServiceKeys.Apple),
            EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube),
            subjects,
            people,
            guestSuggestions,
            ExtraKeysOn(episode));

    public static string[]? ExtraKeysOn(Episode episode)
    {
        var keys = StreamingServiceCatalog.SearchEncodedKeys
            .Where(key => EpisodeServicePresence.HasUrl(episode, key))
            .ToArray();
        return keys.Length == 0 ? null : keys;
    }
}
