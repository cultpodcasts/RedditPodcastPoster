using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates for Episode JSON cutover dual-keys. Prefer these over filtering on
/// <see cref="Playable.ReleaseSort"/> or <see cref="Playable.ParentRemoved"/> alone — deserialize
/// bridges do not run server-side. SQL mirrors live on <see cref="Playable"/>.
/// </summary>
public static class EpisodeCosmosFilters
{
    /// <summary>
    /// Effective release instant &gt;= <paramref name="since"/> using
    /// <c>(IS_DEFINED(releaseSort) ? releaseSort : release)</c>.
    /// </summary>
    public static Expression<Func<Episode, bool>> ReleasedOnOrAfter(DateTime since) =>
        e => (e.ReleaseSort.IsDefined() ? e.ReleaseSort : e.ReleaseCosmosFallback) >= since;

    /// <summary>
    /// Parent publisher is not removed: dual-key <c>parentRemoved</c> and legacy <c>podcastRemoved</c>.
    /// </summary>
    public static Expression<Func<Episode, bool>> ParentNotRemoved { get; } =
        e => (!e.ParentRemoved.IsDefined() || e.ParentRemoved == false || e.ParentRemoved == null) &&
             (!e.LegacyPodcastRemoved.IsDefined() || e.LegacyPodcastRemoved == false ||
              e.LegacyPodcastRemoved == null);

    /// <summary>Combines two Episode predicates with AND (parameter-replacing).</summary>
    public static Expression<Func<Episode, bool>> And(
        Expression<Func<Episode, bool>> left,
        Expression<Func<Episode, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Episode), "e");
        var body = Expression.AndAlso(
            ReplaceParameter(left.Body, left.Parameters[0], parameter),
            ReplaceParameter(right.Body, right.Parameters[0], parameter));
        return Expression.Lambda<Func<Episode, bool>>(body, parameter);
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression from, ParameterExpression to) =>
        new ParameterReplacer(from, to).Visit(body);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
