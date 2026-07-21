using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.People.Models;

namespace RedditPodcastPoster.People.Enrichers;

public interface IEpisodeGuestEnricher
{
    /// <summary>
    /// Union high-confidence matches into <see cref="Episode.Guests"/>.
    /// Never removes guests.
    /// </summary>
    Task<EnrichGuestsResult> EnrichGuests(
        Episode episode,
        GuestEnrichmentOptions? options = null);
}
