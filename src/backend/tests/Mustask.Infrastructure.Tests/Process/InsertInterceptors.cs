using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>A command interceptor that reacts to the first statement inserting into one table, before it reaches the database.</summary>
public abstract class InsertInterceptor(string table) : DbCommandInterceptor
{
    private readonly Regex _insert = new($"INSERT INTO \"?{Regex.Escape(table)}\"?", RegexOptions.IgnoreCase);

    /// <summary>Called for each statement that inserts into the table. Throws to fail it.</summary>
    protected abstract void OnInsert();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Check(command);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(command);

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Check(DbCommand command)
    {
        if (_insert.IsMatch(command.CommandText))
        {
            OnInsert();
        }
    }
}

/// <summary>Stands in for a storage error: fails the insert into one table while armed.</summary>
public sealed class FailingInsertInterceptor(string table) : InsertInterceptor(table)
{
    public bool Armed { get; set; } = true;

    protected override void OnInsert()
    {
        if (Armed)
        {
            throw new InjectedStorageFailure();
        }
    }
}

/// <summary>Stands in for a caller that gives up: cancels the call's token at the insert into one table.</summary>
public sealed class CancellingInsertInterceptor(string table, CancellationTokenSource source) : InsertInterceptor(table)
{
    protected override void OnInsert()
    {
        source.Cancel();
        source.Token.ThrowIfCancellationRequested();
    }
}

/// <summary>The error the interceptor injects, to tell it from any other failure.</summary>
public sealed class InjectedStorageFailure() : Exception("Injected storage failure.");
