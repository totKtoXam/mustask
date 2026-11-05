using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// Records the text of every command a context sends — queries, scalars and non-queries alike — in the order they were sent, so a test
/// can pin a lock statement's shape (<c>ORDER BY … FOR NO KEY UPDATE</c>) or the number of statements (feat-028 plan-backend § 7.4, § 9.4).
/// </summary>
public sealed class SqlLogInterceptor : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);

        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
