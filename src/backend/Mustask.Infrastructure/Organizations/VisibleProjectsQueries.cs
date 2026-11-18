using System.Linq.Expressions;
using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The one way a read of several projects' rows applies the caller's sight (feat-062, BR-TD-17): a predicate in the statement itself,
/// before any grouping, count, ordering or paging, so what is counted, paged or cursored is only what the caller sees. The
/// administrator's <see cref="VisibleProjects.All"/> adds nothing. The ids reach the database as one array parameter, so the
/// statement's text does not change with the number of visible projects.
/// </summary>
internal static class VisibleProjectsQueries
{
    /// <summary>Keeps the rows whose project (by <paramref name="projectId"/>) the caller sees.</summary>
    public static IQueryable<T> WhereVisible<T>(this IQueryable<T> source, VisibleProjects visible, Expression<Func<T, Guid>> projectId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(visible);
        ArgumentNullException.ThrowIfNull(projectId);

        if (visible.Ids is not { } ids)
        {
            return source;
        }

        // A member of a captured object, so EF sends the array as a parameter instead of inlining it.
        var box = new IdBox(ids.ToArray());
        var array = Expression.Property(Expression.Constant(box), nameof(IdBox.Ids));
        var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], array, projectId.Body);

        return source.Where(Expression.Lambda<Func<T, bool>>(contains, projectId.Parameters));
    }

    private sealed class IdBox(Guid[] ids)
    {
        public Guid[] Ids { get; } = ids;
    }
}
