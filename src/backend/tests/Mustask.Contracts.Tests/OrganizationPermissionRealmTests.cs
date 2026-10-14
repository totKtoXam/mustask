using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// US-2 (mod-001-process-seed) · "The read's code is registered, and nothing
/// grants it yet" (BR-M2-10, status.md D-8). <see cref="KeycloakRealmRolesTests"/>
/// keeps the realm's client roles equal to <c>PermissionCatalog.InstanceCodes</c>;
/// this file closes the other side: no organization-scoped code is declared in
/// the realm import at all. The scan is deliberately wider than the known
/// places (client roles, realm roles, composites, group mappings, default
/// roles): it walks every property name and every string value of every realm
/// file, so a place nobody thought of is covered too.
/// </summary>
public sealed class OrganizationPermissionRealmTests
{
    [Fact]
    public void RealmImports_MentionNoOrganizationScopedCode()
    {
        var realms = RealmFiles();

        Assert.NotEmpty(realms);

        foreach (var file in realms)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));

            var mentions = FindMentions(document.RootElement, PermissionCatalog.OrganizationCodes);

            Assert.True(
                mentions.Count == 0,
                $"The realm import `{file.Name}` mentions organization-scoped codes: {string.Join(", ", mentions)}. "
                + "These codes are never declared in the realm (BR-M2-10): no role, composite, group or default role grants them.");
        }
    }

    /// <summary>
    /// The walk is not vacuous: it reaches the strings it must look at. A scan
    /// that quietly stopped reading would pass exactly like a clean realm.
    /// </summary>
    [Fact]
    public void RealmScan_ReachesTheDictionaryCodesTheRealmDoesDeclare()
    {
        foreach (var file in RealmFiles())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));

            var found = FindMentions(document.RootElement, PermissionCatalog.InstanceCodes);

            Assert.Equal(
                PermissionCatalog.InstanceCodes.Order(StringComparer.Ordinal),
                found.Distinct().Order(StringComparer.Ordinal));
        }
    }

    [Theory]
    [InlineData("""{ "roles": { "client": { "mustask-api": [ { "name": "process.definition.read" } ] } } }""", "a client role")]
    [InlineData("""{ "roles": { "realm": [ { "composites": { "client": { "mustask-api": [ "process.definition.read" ] } } } ] } }""", "a composite")]
    [InlineData("""{ "groups": [ { "realmRoles": [ "process.definition.read" ] } ] }""", "a group mapping")]
    [InlineData("""{ "defaultRole": { "composites": { "realm": [ "process.definition.read" ] } } }""", "a default role")]
    [InlineData("""{ "process.definition.read": true }""", "a property name")]
    [InlineData("""{ "note": "grants process.definition.read to everyone" }""", "a string that only contains the code")]
    public void FindMentions_CodeInAnyPlace_IsFound(string json, string place)
    {
        using var document = JsonDocument.Parse(json);

        var mentions = FindMentions(document.RootElement, PermissionCatalog.OrganizationCodes);

        Assert.True(mentions.Count > 0, $"A mention in {place} was not found.");
    }

    [Fact]
    public void FindMentions_RealmWithoutTheCode_FindsNothing()
    {
        using var document = JsonDocument.Parse("""{ "roles": { "client": { "mustask-api": [ { "name": "dictionaries.languages.read" } ] } } }""");

        Assert.Empty(FindMentions(document.RootElement, PermissionCatalog.OrganizationCodes));
    }

    private static IReadOnlyList<FileInfo> RealmFiles() =>
        new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms"))
            .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>The codes found in any property name or string value under <paramref name="element"/>, as substrings.</summary>
    private static List<string> FindMentions(JsonElement element, IEnumerable<string> codes)
    {
        var found = new List<string>();
        var codeList = codes.ToList();

        void Visit(JsonElement current)
        {
            switch (current.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in current.EnumerateObject())
                    {
                        Match(property.Name);
                        Visit(property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in current.EnumerateArray())
                    {
                        Visit(item);
                    }

                    break;
                case JsonValueKind.String:
                    Match(current.GetString()!);
                    break;
            }
        }

        void Match(string text) =>
            found.AddRange(codeList.Where(code => text.Contains(code, StringComparison.Ordinal)));

        Visit(element);

        return found;
    }
}
