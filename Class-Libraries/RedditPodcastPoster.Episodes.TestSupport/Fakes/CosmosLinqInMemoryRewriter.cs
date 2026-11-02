using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>
/// Cosmos LINQ helpers like <see cref="CosmosLinqExtensions.IsDefined"/> only translate
/// server-side. In-memory fakes must rewrite them before <see cref="Expression.Compile"/>.
/// CLR objects always have the member: treat <c>IsDefined</c> as <c>true</c>.
/// Missing dictionary keys (and a null dictionary) are treated as Cosmos undefined so
/// <c>Services[key].Member</c> evaluates to the member's default instead of throwing.
/// </summary>
public static class CosmosLinqInMemoryRewriter
{
    public static Expression<Func<T, bool>> ForInMemory<T>(Expression<Func<T, bool>> expression) =>
        Expression.Lambda<Func<T, bool>>(new Visitor().Visit(expression.Body)!, expression.Parameters);

    public static Expression<Func<T, TProjection>> ForInMemory<T, TProjection>(
        Expression<Func<T, TProjection>> expression) =>
        Expression.Lambda<Func<T, TProjection>>(new Visitor().Visit(expression.Body)!, expression.Parameters);

    private sealed class Visitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(CosmosLinqExtensions) &&
                node.Method.Name == nameof(CosmosLinqExtensions.IsDefined))
            {
                return Expression.Constant(true);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is MethodCallExpression { Method.Name: "get_Item", Object: { } dictionary } indexer &&
                dictionary.Type.IsGenericType &&
                dictionary.Type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var visitedDictionary = Visit(dictionary);
                var visitedKey = Visit(indexer.Arguments[0]);
                var visited = (MemberExpression)base.VisitMember(node);
                var containsKey = Expression.Call(
                    visitedDictionary, dictionary.Type.GetMethod("ContainsKey")!, visitedKey);
                var hasKey = Expression.AndAlso(
                    Expression.NotEqual(visitedDictionary, Expression.Constant(null, dictionary.Type)),
                    containsKey);
                return Expression.Condition(hasKey, visited, Expression.Default(node.Type));
            }

            return base.VisitMember(node);
        }
    }
}
