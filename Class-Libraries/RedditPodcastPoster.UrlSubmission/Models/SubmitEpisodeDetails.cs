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
    bool BBC = false,
    bool InternetArchive = false,
    PersonMatch[]? People = null,
    PersonMatch[]? GuestSuggestions = null,
    bool Vimeo = false,
    bool Netflix = false,
    bool AmazonPrime = false,
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
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.BbcIplayer) ||
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.BbcSounds),
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.InternetArchive),
            people,
            guestSuggestions,
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.Vimeo),
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.Netflix),
            EpisodeServicePresence.HasUrl(episode, StreamingServiceKeys.AmazonPrime),
            ExtraKeysOn(episode));

    public static string[]? ExtraKeysOn(Episode episode)
    {
        var keys = StreamingServiceCatalog.SearchEncodedKeys
            .Where(key => EpisodeServicePresence.HasUrl(episode, key))
            .ToArray();
        return keys.Length == 0 ? null : keys;
    }
}
