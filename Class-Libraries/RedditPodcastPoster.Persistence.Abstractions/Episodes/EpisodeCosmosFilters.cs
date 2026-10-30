using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates shared by Episode queries. SQL mirrors live on <see cref="Playable"/>.
/// </summary>
public static class EpisodeCosmosFilters
{
    /// <summary>
    /// Release instant (<c>releaseSort</c>) &gt;= <paramref name="since"/>.
    /// </summary>
    public static Expression<Func<Episode, bool>> ReleasedOnOrAfter(DateTime since) =>
        e => e.ReleaseSort >= since;

    /// <summary>
    /// Parent publisher is not removed: <c>parentRemoved</c> absent, null or false.
    /// </summary>
    public static Expression<Func<Episode, bool>> ParentNotRemoved { get; } =
        e => !e.ParentRemoved.IsDefined() || e.ParentRemoved == false || e.ParentRemoved == null;

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
