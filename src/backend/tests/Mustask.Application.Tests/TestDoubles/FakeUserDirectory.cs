using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.TestDoubles;

/// <summary>
/// An in-memory <see cref="IUserDirectory"/> with mod-014's contract (api-contract Part A): a lookup of more than 500 ids and a
/// search restricted to more than 500 are refused, a text under 2 characters answers nobody, a text over 100 is refused,
/// at most 20 users are answered in <see cref="DirectoryUserOrder"/> and an empty restriction answers nobody. It records the
/// size of every call, so a test can pin the batching of a consumer (BR-UP-17).
/// </summary>
public sealed class FakeUserDirectory : IUserDirectory
{
    private readonly Dictionary<string, DirectoryUser> _users = new(StringComparer.Ordinal);

    /// <summary>The number of ids of every <see cref="LookupAsync"/> call, in order.</summary>
    public List<int> LookupSizes { get; } = [];

    /// <summary>The number of restricted ids of every <see cref="SearchAsync"/> call, in order (<c>-1</c>: no restriction).</summary>
    public List<int> SearchSizes { get; } = [];

    public FakeUserDirectory Add(string id, string displayName, string email = "", bool isActive = true)
    {
        _users[id] = new DirectoryUser(id, displayName, email, isActive);

        return this;
    }

    public Task<UserSearchResult> SearchAsync(UserSearch search, CancellationToken cancellationToken)
    {
        SearchSizes.Add(search.AmongIds?.Count ?? -1);

        var text = search.Text.Trim();

        if (text.Length > UserDirectoryLimits.SearchTextMaxLength)
        {
            throw Refusal("text");
        }

        if (search.AmongIds is { Count: > UserDirectoryLimits.IdsPerRead })
        {
            throw Refusal("amongIds");
        }

        if (text.Length < UserDirectoryLimits.SearchTextMinLength || search.AmongIds is { Count: 0 })
        {
            return Task.FromResult(new UserSearchResult([], MoreMatched: false));
        }

        var matches = _users.Values
            .Where(user => user.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Where(user => search.IncludeInactive || user.IsActive)
            .Where(user => search.AmongIds is null || search.AmongIds.Contains(user.Id))
            .Order(DirectoryUserOrder.Comparer)
            .ToList();

        return Task.FromResult(new UserSearchResult(
            matches.Take(UserDirectoryLimits.SearchAnswerSize).ToList(),
            matches.Count > UserDirectoryLimits.SearchAnswerSize));
    }

    public Task<UserLookupResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        LookupSizes.Add(ids.Count);

        if (ids.Count > UserDirectoryLimits.IdsPerRead)
        {
            throw Refusal("ids");
        }

        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        var known = distinct.Where(_users.ContainsKey).ToDictionary(id => id, id => _users[id], StringComparer.Ordinal);

        return Task.FromResult(new UserLookupResult(known, distinct.Where(id => !known.ContainsKey(id)).ToList()));
    }

    private static ValidationException Refusal(string field) =>
        new(
            UserDirectoryErrorCodes.ValidationFailed,
            new Dictionary<string, IReadOnlyList<FieldError>> { [field] = [new FieldError(UserDirectoryErrorCodes.ValueTooLong)] });
}
