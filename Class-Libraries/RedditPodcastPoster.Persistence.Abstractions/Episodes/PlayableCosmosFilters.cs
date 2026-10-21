using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates for any playable container (TvShowEpisode, NewsReport, Film).
/// Same dual-key release rule as <see cref="EpisodeCosmosFilters.ReleasedOnOrAfter"/>.
/// </summary>
public static class PlayableCosmosFilters
{
    public static Expression<Func<TPlayable, bool>> ReleasedOnOrAfter<TPlayable>(DateTime since)
        where TPlayable : Playable =>
        e => (e.ReleaseSort.IsDefined() ? e.ReleaseSort : e.ReleaseCosmosFallback) >= since;
}
