using System.Reflection;
using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Tests.Abstractions.UserDirectory;

/// <summary>
/// mod-014 US-1 "A search by part of a name finds an active user": a user in
/// an answer is exactly four values — nothing else about a user is offered
/// (BR-UP-2, BR-UP-11). The read model's surface is fixed by api-contract.md
/// Part A (D-2 (3)); this pins it.
/// </summary>
public sealed class DirectoryUserShapeTests
{
    [Fact]
    public void DirectoryUser_HasExactlyTheFourContractValues()
    {
        var properties = typeof(DirectoryUser)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract")
            .Select(property => (property.Name, property.PropertyType))
            .ToArray();

        Assert.Equal(
            [
                (nameof(DirectoryUser.Id), typeof(string)),
                (nameof(DirectoryUser.DisplayName), typeof(string)),
                (nameof(DirectoryUser.Email), typeof(string)),
                (nameof(DirectoryUser.IsActive), typeof(bool)),
            ],
            properties);
    }

    [Fact]
    public void IUserDirectory_HasTwoReadsAndNoOther()
    {
        var methods = typeof(IUserDirectory).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal([nameof(IUserDirectory.LookupAsync), nameof(IUserDirectory.SearchAsync)], methods);
    }

    [Fact]
    public void UserSearch_DefaultsAreActiveOnlyOverTheWholeProjection()
    {
        var search = new UserSearch("анна");

        Assert.False(search.IncludeInactive);
        Assert.Null(search.AmongIds);
    }
}
