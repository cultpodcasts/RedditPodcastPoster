using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Models;

namespace RedditPodcastPoster.People;

public interface IEpisodeGuestEnricher
{
    /// <summary>
    /// Union high-confidence matches into <see cref="Episode.Guests"/>.
    /// Never removes guests; never touches <see cref="Episode.TwitterHandles"/> /
    /// <see cref="Episode.BlueskyHandles"/>.
    /// </summary>
    Task<EnrichGuestsResult> EnrichGuests(
        Episode episode,
        GuestEnrichmentOptions? options = null);
}
