namespace PlotTwist.Infrastructure.Persistence;

using System.Linq.Expressions;
using PlotTwist.Domain.Search;

// MovieCriteria -> expression tree the connector translates into SQL WHERE.
// Only the conditions that are set go in: a "from == null ||" style expression does not translate.
internal static class MovieCriteriaFilter
{
    public static Expression<Func<MovieRecord, bool>>? ToExpression(MovieCriteria criteria)
    {
        var conditions = new List<Expression<Func<MovieRecord, bool>>>();
        if (criteria.ReleasedFrom is { Value: var from }) conditions.Add(m => m.Year >= from);
        if (criteria.ReleasedTo is { Value: var to }) conditions.Add(m => m.Year <= to);
        if (criteria.Genre is { Value: var genre }) conditions.Add(m => m.Genres.Contains(genre));

        if (conditions.Count == 0)
            return null;

        var parameter = Expression.Parameter(typeof(MovieRecord), "m");
        var body = conditions
            .Select(c => new ReplaceParameter(c.Parameters[0], parameter).Visit(c.Body))
            .Aggregate(Expression.AndAlso);
        return Expression.Lambda<Func<MovieRecord, bool>>(body, parameter);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
