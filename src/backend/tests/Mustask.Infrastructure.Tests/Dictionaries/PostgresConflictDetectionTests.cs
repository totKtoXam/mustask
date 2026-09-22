using Mustask.Infrastructure.Dictionaries;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Dictionaries;

/// <summary>
/// <see cref="LanguageRepository.SaveAsync"/> needs to tell a genuine
/// unique-index race (Postgres 23505, a real, constructible
/// <see cref="PostgresException"/>) apart from any other
/// <see cref="DbUpdateException"/> -- exercised here without a database,
/// since <c>PostgresException</c> has a public constructor.
/// </summary>
public sealed class PostgresConflictDetectionTests
{
    private static PostgresException UniqueViolation(string? constraintName = null) =>
        new(messageText: "duplicate key value violates unique constraint", severity: "ERROR",
            invariantSeverity: "ERROR", sqlState: PostgresErrorCodes.UniqueViolation, constraintName: constraintName);

    private static PostgresException OtherPostgresError() =>
        new(messageText: "some other failure", severity: "ERROR",
            invariantSeverity: "ERROR", sqlState: PostgresErrorCodes.NotNullViolation);

    [Fact]
    public void IsUniqueViolation_WrapsA23505PostgresException_ReturnsTrue()
    {
        var ex = new DbUpdateException("save failed", UniqueViolation());

        Assert.True(PostgresConflictDetection.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_WrapsADifferentPostgresError_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", OtherPostgresError());

        Assert.False(PostgresConflictDetection.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_NoInnerException_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed");

        Assert.False(PostgresConflictDetection.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_InnerExceptionIsNotPostgresException_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", new InvalidOperationException("unrelated"));

        Assert.False(PostgresConflictDetection.IsUniqueViolation(ex));
    }

    /// <summary>`languages` has two unique indexes -- the code and the "exactly one default row" one -- and only a matching `ConstraintName` tells a violation of one apart from the other.</summary>
    [Fact]
    public void IsUniqueViolationOnConstraint_MatchingConstraintName_ReturnsTrue()
    {
        var ex = new DbUpdateException("save failed", UniqueViolation("ix_languages_code"));

        Assert.True(PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_code"));
    }

    [Fact]
    public void IsUniqueViolationOnConstraint_DifferentConstraintName_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", UniqueViolation("ix_languages_is_default"));

        Assert.False(PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_code"));
    }

    [Fact]
    public void IsUniqueViolationOnConstraint_NoConstraintNameOnTheException_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", UniqueViolation());

        Assert.False(PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_code"));
    }

    [Fact]
    public void IsUniqueViolationOnConstraint_NotAUniqueViolation_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", OtherPostgresError());

        Assert.False(PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_code"));
    }
}
