using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// Whether a podcast change could have altered an episode's D1 search document without the episode itself being saved
/// (#1475). The episode document takes three things from its podcast: <c>seriesName</c> (the trimmed name),
/// <c>publisherSearchTerms</c> and <c>podcastAppleId</c>; eligibility takes <c>removed</c>, which the reconcile already
/// handles as an exclusion. Each episode keeps a denormalised projection of the name and search terms
/// (<see cref="Episode.SetPodcastProperties"/>), refreshed when a rename is hydrated. So when that projection still
/// matches the live podcast, a newer podcast <c>_ts</c> is bookkeeping and the indexed content is unchanged; when it
/// does not, the document may be behind and is re-sent.
/// <para>
/// <c>podcastAppleId</c> is not projected onto episodes, so a change to only that field is not detected here. It is
/// picked up the next time each episode is saved.
/// </para>
/// </summary>
public static class D1SearchEpisodeParentProjection
{
    public static bool IsBehind(Podcast podcast, Episode episode)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episode);
        return !string.Equals(episode.PodcastName?.Trim(), podcast.Name.Trim(), StringComparison.Ordinal)
               || !string.Equals(NullIfEmpty(episode.PublisherSearchTerms), NullIfEmpty(podcast.SearchTerms),
                   StringComparison.Ordinal);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
