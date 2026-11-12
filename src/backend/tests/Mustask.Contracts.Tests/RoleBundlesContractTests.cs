using System.Text;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// <c>contracts/access/role-bundles.json</c> (mod-019 api-contract § 9): the instance table, the organization bundles, the scoped
/// bundles and the catalog's source and resource scope, machine-readable, so the panel imports one file instead of mirroring the
/// tables by hand. The file is generated from the domain and committed; a drift fails the build.
/// Regenerate: <c>MUSTASK_UPDATE_ROLE_BUNDLES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests</c>.
/// </summary>
public sealed class RoleBundlesContractTests
{
    private static string ContractPath => Path.Combine(RepositoryLayout.Root.FullName, "contracts", "access", "role-bundles.json");

    [Fact]
    public void RoleBundlesFile_MatchesTheDomainTables()
    {
        var expected = Normalize(Render());

        if (Environment.GetEnvironmentVariable("MUSTASK_UPDATE_ROLE_BUNDLES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ContractPath)!);
            File.WriteAllText(ContractPath, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return;
        }

        Assert.True(File.Exists(ContractPath), "contracts/access/role-bundles.json is missing: MUSTASK_UPDATE_ROLE_BUNDLES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests");
        Assert.True(
            Normalize(File.ReadAllText(ContractPath)) == expected,
            "contracts/access/role-bundles.json drifted from the domain tables: MUSTASK_UPDATE_ROLE_BUNDLES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests");
    }

    [Fact]
    public void RoleBundlesFile_NamesEveryCatalogCodeOnce_AndEveryInstanceRole()
    {
        using var document = JsonDocument.Parse(Render());
        var root = document.RootElement;

        Assert.Equal(
            PermissionCatalog.AllCodes.Order(StringComparer.Ordinal),
            root.GetProperty("catalog").EnumerateArray().Select(entry => entry.GetProperty("code").GetString()!).Order(StringComparer.Ordinal));
        Assert.Equal(
            InstanceRoles.Names.Order(StringComparer.Ordinal),
            root.GetProperty("instanceRoles").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    private static string Render()
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("$comment", "Generated from Mustask.Domain.Identity by RoleBundlesContractTests; do not edit by hand (mod-019 api-contract § 9).");

            writer.WriteStartObject("instanceRoles");

            foreach (var role in InstanceRoles.All)
            {
                WriteCodes(writer, role.Name, role.Codes);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("organizationRoles");

            foreach (var role in Enum.GetValues<OrganizationRole>())
            {
                WriteCodes(writer, OrganizationRoles.ToCode(role), OrganizationRoleBundles.CodesOf(role));
            }

            writer.WriteEndObject();

            writer.WriteStartObject("scopedRoles");

            foreach (var scope in ScopedRoleBundles.GrantScopes)
            {
                writer.WriteStartObject(scope.ToString().ToLowerInvariant());

                foreach (var role in Enum.GetValues<GrantRole>())
                {
                    WriteCodes(writer, GrantRoles.ToCode(role), ScopedRoleBundles.CodesOf(scope, role));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartArray("catalog");

            foreach (var definition in PermissionCatalog.All.OrderBy(definition => definition.Code, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("code", definition.Code);
                writer.WriteString("source", definition.Source.ToString().ToLowerInvariant());
                writer.WriteString("resourceScope", definition.Source == PermissionSource.Instance ? null : PermissionCatalog.ResourceScopeOf(definition.Code).ToString().ToLowerInvariant());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCodes(Utf8JsonWriter writer, string name, IEnumerable<string> codes)
    {
        writer.WriteStartArray(name);

        foreach (var code in codes.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(code);
        }

        writer.WriteEndArray();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
}
