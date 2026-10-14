using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// <see cref="KeycloakRealmRolesTests"/> proves the registry is closed — every
/// permission code exists as a client role and no role invents one — but
/// nothing there pins down *who holds* the dictionary codes. The read code
/// belongs to **every signed-in user**, because the dictionaries (e.g. the list
/// of languages) are reference data every screen may need, and the API is
/// deny-all, so "everyone reads it" has to be spelled as a grant. Since mod-016
/// (BR-M1-29, D-15 (3)) the grant is the realm's **default role**: Keycloak gives
/// it to every user, a user in no group included, and the organization roles
/// `member` and `viewer` no longer exist in the realm. A future realm edit
/// dropping the read code from the default role would leave users with an
/// empty list and a 403 no log explains — the failure this file exists to catch.
/// </summary>
public sealed class DictionaryPermissionsRealmTests
{
    private const string MustaskRealm = "mustask-realm.json";
    private const string ResourceClient = "mustask-api";
    private const string DictionariesAdminRole = "dictionaries-admin";
    private const string InstanceAdminRole = "instance-admin";
    private const string DefaultRole = "default-roles-mustask";

    /// <summary>
    /// Roles that grant one instance right and nothing else (mod-002 D-u): an
    /// add-on to the default role, never a role of their own. A user holds
    /// one of them beside the default role (which reads the dictionaries for
    /// everyone), so they carry no dictionary code.
    /// <c>organization-creator</c> carries the create right only (BR-M1-19:
    /// "grants nothing else").
    /// </summary>
    private static readonly HashSet<string> GrantOnlyRoles = new(StringComparer.Ordinal)
    {
        "organization-creator",
    };

    private static readonly string[] ReadCodes =
    [
        PermissionCatalog.DictionariesLanguagesRead,
    ];

    private static readonly string[] WriteCodes =
    [
        PermissionCatalog.DictionariesLanguagesWrite,
    ];

    /// <summary>
    /// US-8 · "Every signed-in user reads the dictionaries" (BR-M1-29): the realm's default role holds every dictionary read code,
    /// so a user in no group, a member of /Создатели организаций only and a member of /Администраторы справочников all read.
    /// </summary>
    [Fact]
    public void TheDefaultRole_HoldsEveryDictionaryReadCode_SoEverySignedInUserReads()
    {
        var byRole = PermissionsByRole();

        Assert.True(byRole.ContainsKey(DefaultRole), $"В realm `mustask` нет роли `{DefaultRole}` в roles.realm.");

        var missing = ReadCodes.Where(code => !byRole[DefaultRole].Contains(code)).ToList();

        Assert.True(
            missing.Count == 0,
            $"Роль по умолчанию `{DefaultRole}` не держит права чтения справочников: {string.Join(", ", missing)}. "
            + "`dictionaries.*.read` получает каждый вошедший пользователь — справочники нужны всем "
            + "экранам, а политика API deny-all.");
    }

    /// <summary>The roles that carry a write also carry the reads — an administrator who cannot read the list cannot edit it.</summary>
    [Fact]
    public void EveryRoleHoldingAWriteCode_AlsoHoldsTheReadCodes()
    {
        var writers = PermissionsByRole().Where(pair => WriteCodes.Any(code => pair.Value.Contains(code))).ToList();

        Assert.NotEmpty(writers);
        Assert.All(writers, pair => Assert.All(ReadCodes, code => Assert.Contains(code, pair.Value)));
    }

    /// <summary>
    /// The exception list is not a hiding place: every role on it exists in the
    /// realm and carries no dictionary code at all, so a panel role cannot be
    /// slipped onto it to escape the rule above.
    /// </summary>
    [Fact]
    public void GrantOnlyRoles_ExistAndHoldNoDictionaryCode()
    {
        var byRole = PermissionsByRole();

        foreach (var role in GrantOnlyRoles)
        {
            Assert.True(byRole.ContainsKey(role), $"The grant-only role `{role}` is not in realm `mustask`.");
            Assert.DoesNotContain(byRole[role], code => ReadCodes.Contains(code) || WriteCodes.Contains(code));
        }
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
    /// `...write` only for `instance-admin` (which must hold every instance code)
    /// and for the dictionaries administrator — a third role quietly gaining the
    /// right to edit the dictionaries is a decision, not an edit. In particular the
    /// default role, which every user holds, never does.
    /// </summary>
    [Fact]
    public void WriteCodes_AreHeldOnlyByTheInstanceAdminAndTheDictionariesAdministrator()
    {
        var unexpected = PermissionsByRole()
            .Where(pair => pair.Key is not (InstanceAdminRole or DictionariesAdminRole))
            .Where(pair => WriteCodes.Any(code => pair.Value.Contains(code)))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            $"Право изменения справочников выдано ролям, кроме {InstanceAdminRole} и {DictionariesAdminRole}: {string.Join(", ", unexpected)}.");
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
