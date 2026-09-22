using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// <see cref="KeycloakRealmRolesTests"/> proves the registry is closed — every
/// permission code exists as a client role and no role invents one — but
/// nothing there pins down *who holds* the dictionary codes. The read code
/// belongs to **every** panel role, because the dictionaries (e.g. the list
/// of languages) are reference data every screen may need, and the API is
/// deny-all, so "everyone reads it" has to be spelled as a grant. A future
/// realm edit dropping the read code from `member` or `viewer` would leave
/// those users with an empty list and a 403 no log explains — the failure
/// this file exists to catch.
/// </summary>
public sealed class DictionaryPermissionsRealmTests
{
    private const string MustaskRealm = "mustask-realm.json";
    private const string ResourceClient = "mustask-api";
    private const string DictionariesAdminRole = "dictionaries-admin";

    private static readonly string[] ReadCodes =
    [
        PermissionCatalog.DictionariesLanguagesRead,
    ];

    private static readonly string[] WriteCodes =
    [
        PermissionCatalog.DictionariesLanguagesWrite,
    ];

    /// <summary>Every dictionary read code in every realm role of the panel.</summary>
    [Fact]
    public void EveryPanelRole_HoldsEveryDictionaryReadCode()
    {
        var rolesWithoutRead = PermissionsByRole()
            .Where(pair => ReadCodes.Any(code => !pair.Value.Contains(code)))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            rolesWithoutRead.Count == 0,
            $"Роли realm `mustask` без права чтения справочников: {string.Join(", ", rolesWithoutRead)}. "
            + "`dictionaries.*.read` входит в каждую роль панели — справочники нужны всем "
            + "экранам, а политика API deny-all.");
    }

    /// <summary>The composite dictionaries administrator exists and holds every dictionary code.</summary>
    [Fact]
    public void DictionariesAdminRole_HoldsEveryDictionaryCode()
    {
        var byRole = PermissionsByRole();

        Assert.True(
            byRole.ContainsKey(DictionariesAdminRole),
            $"В realm `mustask` нет композитной роли `{DictionariesAdminRole}`.");

        foreach (var code in ReadCodes.Concat(WriteCodes))
        {
            Assert.Contains(code, byRole[DictionariesAdminRole]);
        }
    }

    /// <summary>
    /// `...write` only for `admin` (which must hold everything) and
    /// for the dictionaries administrator — a third role quietly gaining the
    /// right to edit the dictionaries is a decision, not an edit.
    /// </summary>
    [Fact]
    public void WriteCodes_AreHeldOnlyByAdminAndTheDictionariesAdministrator()
    {
        var unexpected = PermissionsByRole()
            .Where(pair => pair.Key is not ("admin" or DictionariesAdminRole))
            .Where(pair => WriteCodes.Any(code => pair.Value.Contains(code)))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            $"Право изменения справочников выдано ролям, кроме admin и {DictionariesAdminRole}: {string.Join(", ", unexpected)}.");
    }

    /// <summary>Every dictionary code is in the catalog the API checks against.</summary>
    [Fact]
    public void PermissionCatalog_DeclaresEveryDictionaryCode()
    {
        foreach (var code in ReadCodes.Concat(WriteCodes))
        {
            Assert.Contains(code, PermissionCatalog.AllCodes);
        }
    }

    private static Dictionary<string, HashSet<string>> PermissionsByRole()
    {
        var path = Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", MustaskRealm);
        Assert.True(File.Exists(path), $"Не найден realm-import: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement
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
    }
}
