using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates for catalogue publishers (TvShow, NewsOrganisation).
/// </summary>
public static class PublisherCosmosFilters
{
    /// <summary>
    /// Not removed, and <c>latestReleased</c> is on or after <paramref name="since"/>.
    /// Same recently-active window the podcast candidate list uses.
    /// </summary>
    public static Expression<Func<TPublisher, bool>> ReleasedOnOrAfter<TPublisher>(DateTime since)
        where TPublisher : Publisher =>
        x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
             x.LatestReleased.IsDefined() &&
             x.LatestReleased != null &&
             x.LatestReleased >= since;
}
