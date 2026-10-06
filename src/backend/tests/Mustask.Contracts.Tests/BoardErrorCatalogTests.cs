using System.Reflection;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Domain.Boards;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// The boards module's error codes against its catalog file (mod-006
/// api-contract § 4, spec § Error codes): each code is in
/// <c>contracts/errors/boards.json</c> with the contract's status, params and
/// Russian text verbatim, and a Kazakh text of its own marked for review.
/// </summary>
public sealed class BoardErrorCatalogTests
{
    /// <summary>api-contract.md § 4 (mod-006), row by row: code, status, params and the Russian text.</summary>
    public static TheoryData<string, int, string, string> ContractTable() => new()
    {
        { BoardErrorCodes.ReorderStale, 409, "", "Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение." },
        { BoardErrorCodes.LevelNotFound, 404, "", "Уровень бэклога не найден." },
        { BoardErrorCodes.ValidationFailed, 400, "", "Проверьте правильность запроса." },
        { BoardErrorCodes.PositionInvalid, 400, "", "Некорректная позиция перемещения." },
    };

    // mod-006 api-contract § 4: every code is registered with its status, params, the contract's ru text and a kk text.
    [Theory]
    [MemberData(nameof(ContractTable))]
    public void Code_IsInTheBoardsCatalogAsTheContractStates(string code, int status, string parameters, string ru)
    {
        var found = CatalogFixture.Catalog.TryGet(code, out var entry);

        Assert.True(found, $"{code} is not in the error catalog.");
        Assert.Equal("boards", entry!.Module);
        Assert.Equal(ErrorCatalogOrigin.Slice, entry.Origin);
        Assert.Equal(new[] { status }, entry.Statuses);
        Assert.Equal(parameters, string.Join(",", entry.Params.Keys.Order(StringComparer.Ordinal)));
        Assert.False(entry.Deprecated);
        Assert.Equal(ru, entry.Ru);
        Assert.False(string.IsNullOrWhiteSpace(entry.Kk));
        Assert.NotEqual(entry.Ru, entry.Kk);
        Assert.True(entry.KkNeedsReview, $"{code}: the Kazakh text is marked for review.");
    }

    // The table above is the whole registry: no constant of BoardErrorCodes is left out of it, and none is invented.
    [Fact]
    public void ContractTable_CoversEveryConstantOfBoardErrorCodes()
    {
        var constants = typeof(BoardErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var table = ContractTable().Select(row => (string)row[0]).Order(StringComparer.Ordinal);

        Assert.Equal(constants, table);
        Assert.True(typeof(BoardErrorCodes).IsDefined(typeof(ErrorCodesAttribute)), "The scanner sees only classes marked [ErrorCodes].");
    }

    // spec § Error codes: the generated reference lists the stale reorder's code.
    [Fact]
    public void ReorderStale_IsListedInTheGeneratedErrorCodeReference()
    {
        var reference = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "docs", "error-codes.md"));
        var row = reference.Split('\n').SingleOrDefault(line => line.StartsWith("|boards.backlog.reorder_stale|", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("|409|", row, StringComparison.Ordinal);
        Assert.Contains("Не удалось переместить: бэклог изменился.", row, StringComparison.Ordinal);
    }
}
