using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure.Dictionaries;

/// <summary>
/// A handler's own "is the value free" pre-check and the insert that follows
/// it are two round-trips -- a second, concurrent write landing in between
/// sees a free value, then hits the storage-level unique index at save time
/// (Postgres 23505). <see cref="LanguageRepository"/> (a language code) has
/// exactly this race, and so will any future repository with a unique
/// business key; the predicate is shared so every `catch` clause agrees on
/// what a unique-violation looks like, the same reasoning as the concurrency
/// backstop next to it (ADR-0010).
/// </summary>
internal static class PostgresConflictDetection
{
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// <c>languages</c> carries two unique indexes
    /// (`LanguageConfiguration`) -- `ix_languages_code` (a free-standing
    /// code) and the filtered `ix_languages_is_default` (at most one
    /// default row). <see cref="IsUniqueViolation"/> alone cannot tell them
    /// apart, so a race on the second index would be reported as the first
    /// index's registry code. Narrows the check to a named constraint via
    /// <see cref="PostgresException.ConstraintName"/>.
    /// </summary>
    public static bool IsUniqueViolationOnConstraint(DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && string.Equals(pg.ConstraintName, constraintName, StringComparison.Ordinal);

    /// <summary>
    /// A foreign key (23503) named <paramref name="constraintName"/> refused the write: the row it points at is gone, or
    /// went while the insert ran. mod-016's team membership onto the organization membership is the first (BR-M1-10).
    /// </summary>
    public static bool IsForeignKeyViolationOnConstraint(DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } pg
        && string.Equals(pg.ConstraintName, constraintName, StringComparison.Ordinal);
}
