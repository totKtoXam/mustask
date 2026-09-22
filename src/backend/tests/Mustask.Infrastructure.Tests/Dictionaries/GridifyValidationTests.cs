using Gridify;
using Mustask.Domain.Dictionaries;
using Mustask.Infrastructure.Dictionaries;

namespace Mustask.Infrastructure.Tests.Dictionaries;

/// <summary>
/// A malformed `filter`/`orderBy` -- one Gridify's own
/// parser cannot make sense of at all, as opposed to a syntactically valid
/// expression naming a field outside the whitelist -- used to reach the
/// caller as a raw <c>GridifyFilteringException</c>/
/// <c>GridifyOrderingException</c> and answer 500. Exercises
/// <see cref="GridifyValidation.EnsureFieldsAreWhitelisted{T}"/> directly, no
/// database needed: the parse happens in memory.
/// </summary>
public sealed class GridifyValidationTests
{
    private sealed record Row(string Code, bool IsActive);

    private static GridifyMapper<Row> Mapper()
    {
        var mapper = new GridifyMapper<Row>(autoGenerateMappings: false);
        mapper.AddMap("code", r => r.Code);
        mapper.AddMap("isActive", r => r.IsActive);
        return mapper;
    }

    private static GridifyQuery Query(string? filter = null, string? orderBy = null) =>
        new() { Filter = filter, OrderBy = orderBy, Page = 1, PageSize = 10 };

    [Fact]
    public void EnsureFieldsAreWhitelisted_UnbalancedParenthesesInFilter_ThrowsInvalidQuerySyntax_NamingFilter()
    {
        var ex = Assert.Throws<DictionaryQueryException>(
            () => GridifyValidation.EnsureFieldsAreWhitelisted(Query(filter: "((("), Mapper()));

        Assert.Equal(DictionaryErrorCodes.InvalidQuerySyntax, ex.Code);
        Assert.Equal("filter", (string)ex.Params["field"].Value);
    }

    /// <summary>
    /// Unlike `filter`, Gridify's `orderBy` grammar has no operators or
    /// parentheses to fail on -- `SyntaxTree.ParseOrderings` treats the whole
    /// malformed string as one literal member name instead of throwing, so
    /// this correctly falls through to the ordinary whitelist check
    /// (`dictionaries.unknown_query_field`) rather than
    /// `invalid_query_syntax`. The `GridifyOrderingException` catch in
    /// <see cref="GridifyValidation.EnsureFieldsAreWhitelisted{T}"/> stays as
    /// a defensive backstop for whatever `GridifyAsync` itself might throw
    /// once the expression is actually built (<see cref="ExecuteAsync_GridifyOrderingException_AlwaysNamesOrderBy_EvenWithAFilterPresent"/>).
    /// </summary>
    [Fact]
    public void EnsureFieldsAreWhitelisted_UnbalancedParenthesesInOrderBy_FallsThroughToUnknownQueryField()
    {
        var ex = Assert.Throws<DictionaryQueryException>(
            () => GridifyValidation.EnsureFieldsAreWhitelisted(Query(orderBy: "((("), Mapper()));

        Assert.Equal(DictionaryErrorCodes.UnknownQueryField, ex.Code);
        Assert.Equal("(((", (string)ex.Params["field"].Value);
    }

    [Fact]
    public void EnsureFieldsAreWhitelisted_WellFormedFilterWithUnknownField_ThrowsUnknownQueryField_NotInvalidSyntax()
    {
        var ex = Assert.Throws<DictionaryQueryException>(
            () => GridifyValidation.EnsureFieldsAreWhitelisted(Query(filter: "notAField=1"), Mapper()));

        Assert.Equal(DictionaryErrorCodes.UnknownQueryField, ex.Code);
    }

    [Fact]
    public void EnsureFieldsAreWhitelisted_WellFormedFilter_DoesNotThrow()
    {
        GridifyValidation.EnsureFieldsAreWhitelisted(Query(filter: "code=bilateral"), Mapper());
    }

    [Fact]
    public async Task ExecuteAsync_MalformedGridifyException_MapsToInvalidQuerySyntax_NamingFilterWhenFilterIsPresent()
    {
        var query = Query(filter: "code>>1");

        var ex = await Assert.ThrowsAsync<DictionaryQueryException>(
            () => GridifyValidation.ExecuteAsync<int>(() => throw new GridifyFilteringException("bad operator"), query));

        Assert.Equal(DictionaryErrorCodes.InvalidQuerySyntax, ex.Code);
        Assert.Equal("filter", (string)ex.Params["field"].Value);
    }

    [Fact]
    public async Task ExecuteAsync_GridifyOrderingException_AlwaysNamesOrderBy_EvenWithAFilterPresent()
    {
        var query = Query(filter: "code=bilateral", orderBy: "code>>1");

        var ex = await Assert.ThrowsAsync<DictionaryQueryException>(
            () => GridifyValidation.ExecuteAsync<int>(() => throw new GridifyOrderingException("bad operator"), query));

        Assert.Equal("orderBy", (string)ex.Params["field"].Value);
    }

    [Fact]
    public async Task ExecuteAsync_NoGridifyException_ReturnsTheValue()
    {
        var result = await GridifyValidation.ExecuteAsync(() => Task.FromResult(42), Query());

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedException_PropagatesUnchanged()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GridifyValidation.ExecuteAsync<int>(() => throw new InvalidOperationException("unrelated"), Query()));
    }
}
