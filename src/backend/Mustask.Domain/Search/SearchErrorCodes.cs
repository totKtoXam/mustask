using Mustask.Shared.Errors;

namespace Mustask.Domain.Search;

/// <summary>
/// The error codes of module <c>search</c> (ADR-0005; feat-071 api-contract): texts in <c>contracts/errors/search.json</c>. Declared in
/// Domain because the query rules (<see cref="SearchQueryText"/>) are the domain's. The reasons are the field reasons of
/// <see cref="ValidationFailed"/>; no code or parameter ever carries the typed text (BR-GS-10).
/// </summary>
[ErrorCodes]
public static class SearchErrorCodes
{
    /// <summary>Top-level code of every refusal of a search request.</summary>
    public const string ValidationFailed = "search.validation_failed";

    /// <summary>Field reason (<c>query</c>): fewer than <see cref="SearchQueryText.MinLength"/> characters after trimming.</summary>
    public const string QueryTooShort = "search.query_too_short";

    /// <summary>Field reason (<c>query</c>): more than <see cref="SearchQueryText.MaxLength"/> characters.</summary>
    public const string QueryTooLong = "search.query_too_long";

    /// <summary>Field reason (<c>expand</c>): not one of the group kinds.</summary>
    public const string InvalidGroup = "search.invalid_group";
}
