using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Show sizes observed during lookups, keyed by platform and show id, so enrichers can refresh
/// <c>Podcast.SpotifyEpisodeCount</c> / <c>Podcast.AppleEpisodeCount</c>. Platforms are never cross-used.
/// Spotify counts come free from <c>Paging.Total</c>; Apple counts only from a completed full read or a quick
/// catalogue lookup.
/// </summary>
public interface ICatalogueShowEpisodeCounts
{
    void Record(string platform, string showId, int count);

    bool TryGet(string platform, string showId, out int count);
}

/// <inheritdoc />
public sealed class CatalogueShowEpisodeCounts : ICatalogueShowEpisodeCounts
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string, string), int> _counts = new();

    public void Record(string platform, string showId, int count)
    {
        if (count >= 0 && !string.IsNullOrWhiteSpace(showId))
        {
            _counts[(platform, showId)] = count;
        }
    }

    public bool TryGet(string platform, string showId, out int count) =>
        _counts.TryGetValue((platform, showId), out count);
}
