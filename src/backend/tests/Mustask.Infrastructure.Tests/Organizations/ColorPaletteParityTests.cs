using System.Reflection;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Migrations;
using Mustask.Infrastructure.Organizations;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// doc-016 BR-TD-20 (mod-021 review R-3): the colour check constraint and the migration repeat the palette of
/// <see cref="ColorPalette"/>; the three lists must name the same keys in the same order.
/// </summary>
public sealed class ColorPaletteParityTests
{
    private static string[] Expected => [.. ColorPalette.Entries.Select(entry => entry.Key)];

    [Fact]
    public void ColorCheck_ListsThePaletteKeysInOrder() =>
        Assert.Equal(Expected, Keys(OrganizationsConstraints.ColorCheck));

    [Fact]
    public void MigrationPalette_ListsThePaletteKeysInOrder()
    {
        var constant = typeof(TeamsInDepartments).GetField("Palette", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(constant);

        Assert.Equal(Expected, Keys((string)constant!.GetRawConstantValue()!));
    }

    private static string[] Keys(string check)
    {
        var open = check.IndexOf('(', StringComparison.Ordinal);
        var close = check.LastIndexOf(')');

        return [.. check[(open + 1)..close].Split(',').Select(part => part.Trim().Trim('\''))];
    }
}
