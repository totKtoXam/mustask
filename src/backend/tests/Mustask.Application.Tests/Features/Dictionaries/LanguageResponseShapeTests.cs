using System.Reflection;

namespace Mustask.Application.Tests.Features.Dictionaries;

/// <summary>
/// `LanguageResponse` has four separate copies -- `CreateLanguage`,
/// `UpdateLanguage`, `SetDefaultLanguage` each carry their own
/// `LanguageResponse`, and `GetLanguages` its own `LanguageListItemResponse`
/// for the list row -- and nothing held these four together. A field added
/// to one slice's copy would change one route's JSON and silently leave the
/// other three behind.
/// </summary>
public sealed class LanguageResponseShapeTests
{
    private static readonly Type[] Copies =
    [
        typeof(Application.Features.Dictionaries.CreateLanguage.LanguageResponse),
        typeof(Application.Features.Dictionaries.UpdateLanguage.LanguageResponse),
        typeof(Application.Features.Dictionaries.SetDefaultLanguage.LanguageResponse),
        typeof(Application.Features.Dictionaries.GetLanguages.LanguageListItemResponse),
    ];

    private static List<string> ShapeOf(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.Name != "EqualityContract")
        .Select(p => $"{p.Name}:{p.PropertyType.Name}")
        .Order(StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void EveryCopyOfTheLanguageResponse_HasTheSameShape()
    {
        var shapes = Copies.Select(ShapeOf).ToList();

        Assert.All(shapes, shape => Assert.Equal(shapes[0], shape));
    }

    /// <summary>The fields the contract fixes, named literally -- a rename in all four copies at once is still a contract change.</summary>
    [Fact]
    public void TheLanguageResponse_CarriesExactlyTheFieldsTheContractFixes()
    {
        Assert.Equal(
            ["Code", "Endonym", "IsActive", "IsDefault", "SortOrder", "UpdatedAt", "UpdatedBy", "Version"],
            Copies[0]
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract")
                .Select(p => p.Name)
                .Order(StringComparer.Ordinal)
                .ToList());
    }
}
