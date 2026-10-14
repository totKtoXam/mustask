using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-002 US-1 · "The right is granted and revoked in the console" and "No
/// screen grants the right" (BR-M1-2, BR-M1-19, D-i): the create right
/// <c>organizations.organization.create</c> lives in the realm import, never in a
/// screen. A realm administrator grants it by adding a user to the group
/// «Создатели организаций», whose role carries that one code and nothing else;
/// the instance administrator role carries it too (mod-016 BR-M1-29 renamed the
/// realm role <c>admin</c> to <c>instance-admin</c>: it holds the instance codes
/// only). <see cref="KeycloakRealmRolesTests"/> keeps the code a client role;
/// this file pins who holds it.
/// </summary>
public sealed class OrganizationCreateRightRealmTests
{
    private const string MustaskRealm = "mustask-realm.json";
    private const string ResourceClient = "mustask-api";
    private const string CreatorRole = "organization-creator";
    private const string CreatorGroup = "Создатели организаций";
    private const string InstanceAdminRole = "instance-admin";

    [Fact]
    public void PermissionCatalog_DeclaresTheCreateRight()
    {
        Assert.Equal("organizations.organization.create", PermissionCatalog.OrganizationsOrganizationCreate);
        Assert.Contains(PermissionCatalog.OrganizationsOrganizationCreate, PermissionCatalog.AllCodes);
    }

    // BR-M1-19: the creator role grants the create right and nothing else.
    [Fact]
    public void CreatorRole_CarriesExactlyTheCreateRight()
    {
        var byRole = PermissionsByRole();

        Assert.True(byRole.ContainsKey(CreatorRole), $"Realm `mustask` has no role `{CreatorRole}`.");
        Assert.Equal([PermissionCatalog.OrganizationsOrganizationCreate], byRole[CreatorRole].Order(StringComparer.Ordinal));
    }

    // US-1 · "granted and revoked in the console": membership of this group is the grant.
    [Fact]
    public void CreatorGroup_MapsExactlyTheCreatorRole()
    {
        var group = AllGroups().SingleOrDefault(candidate => candidate.GetProperty("name").GetString() == CreatorGroup);

        Assert.True(group.ValueKind == JsonValueKind.Object, $"Realm `mustask` has no group «{CreatorGroup}».");
        Assert.Equal([CreatorRole], RealmRolesOf(group));
        Assert.False(
            group.TryGetProperty("subGroups", out var subGroups) && subGroups.GetArrayLength() > 0,
            $"The group «{CreatorGroup}» has subgroups: membership of a subgroup would grant the right unseen.");
    }

    // D-i, mod-016 BR-M1-29: the instance administrator role carries every instance code, the create right included.
    [Fact]
    public void InstanceAdminRole_CarriesTheCreateRight()
    {
        Assert.Contains(PermissionCatalog.OrganizationsOrganizationCreate, PermissionsByRole()[InstanceAdminRole]);
    }

    // BR-M1-19: no other role grants the right quietly; a third holder is a decision, not an edit.
    [Fact]
    public void CreateRight_IsHeldOnlyByTheInstanceAdminAndTheCreatorRole()
    {
        var holders = PermissionsByRole()
            .Where(pair => pair.Value.Contains(PermissionCatalog.OrganizationsOrganizationCreate))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal);

        Assert.Equal([InstanceAdminRole, CreatorRole], holders);
    }

    // BR-M1-19: only the administrator group and the creator group give a role holding the right.
    [Fact]
    public void CreateRight_IsGrantedOnlyThroughTheAdministratorAndCreatorGroups()
    {
        var holders = PermissionsByRole()
            .Where(pair => pair.Value.Contains(PermissionCatalog.OrganizationsOrganizationCreate))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

        var granting = AllGroups()
            .Where(group => RealmRolesOf(group).Any(holders.Contains))
            .Select(group => group.GetProperty("name").GetString()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["Администраторы", CreatorGroup], granting);
    }

    private static JsonElement Realm()
    {
        var path = Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", MustaskRealm);
        Assert.True(File.Exists(path), $"The realm import is missing: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    }

    private static Dictionary<string, HashSet<string>> PermissionsByRole() =>
        Realm()
            .GetProperty("roles")
            .GetProperty("realm")
            .EnumerateArray()
            .ToDictionary(
                role => role.GetProperty("name").GetString()!,
                role => role.TryGetProperty("composites", out var composites)
                        && composites.TryGetProperty("client", out var clients)
                        && clients.TryGetProperty(ResourceClient, out var codes)
                    ? codes.EnumerateArray().Select(code => code.GetString()!).ToHashSet(StringComparer.Ordinal)
                    : [],
                StringComparer.Ordinal);

    private static List<JsonElement> AllGroups()
    {
        var groups = new List<JsonElement>();

        void Visit(JsonElement list)
        {
            foreach (var group in list.EnumerateArray())
            {
                groups.Add(group);

                if (group.TryGetProperty("subGroups", out var subGroups))
                {
                    Visit(subGroups);
                }
            }
        }

        Visit(Realm().GetProperty("groups"));

        return groups;
    }

    private static List<string> RealmRolesOf(JsonElement group) =>
        group.TryGetProperty("realmRoles", out var roles)
            ? roles.EnumerateArray().Select(role => role.GetString()!).ToList()
            : [];
}
