using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// ADR-0023: the stand-only,
/// impersonation-capable Keycloak client (`mustask-e2e`) is created by runtime
/// code in <c>Mustask.AppHost</c>
/// (<c>Mustask.AppHost/Provisioning/KeycloakStandProvisioner.cs</c>), never by a
/// realm-import entry — <see cref="Mustask.Contracts.Tests.KeycloakRealmRolesTests"/>'s
/// new facts guard the import files themselves. This class guards the other
/// half of that rule: since the client is created by code, the guarantee "never
/// in production" is only as good as "that code never runs outside a
/// developer's stand" — which for a project that is never deployed
/// (<c>Mustask.AppHost</c> is `dotnet run`-only local orchestration, ADR-0001)
/// means no shipping project may reference or reproduce it.
///
/// Two independent checks, not one, after a backend check walked around
/// the first: <see cref="ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace"/>
/// catches a *reference* to this code (an accidental `using`, a copy that
/// still names its origin in a comment); <see cref="ShippingProject_DoesNotCallKeycloaksAdminApi"/>
/// catches the *capability* — a shipping project touching Keycloak's admin
/// REST surface at all — which is what an independent reimplementation
/// under an unrelated name and namespace cannot avoid needing, however it is
/// written. Verified true today (not assumed): grepping `Mustask.Api` and every
/// inner layer for `admin/realms` or `/admin/` returns nothing —
/// `Mustask.Api`'s only Keycloak traffic is realm discovery/JWKS
/// (<c>Authentication/Readiness</c>), the token endpoint and
/// `end_session_endpoint` (<c>Features/Session/RefreshGrantClient.cs</c>),
/// never the admin API.
/// </summary>
public sealed class StandOnlyKeycloakClientRules
{
    /// <summary>The projects that actually ship — a resource server, a background job, anything a deployment would run.</summary>
    private static readonly string[] ShippingProjects =
    [
        "Mustask.Shared", "Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api",
    ];

    private const string ProvisioningNamespace = "Mustask.AppHost.Provisioning";

    /// <summary>
    /// The literal every one of Keycloak's admin REST endpoints starts with
    /// (`/admin/realms/{realm}/...`) — canonical, not a guess: every call
    /// <c>KeycloakStandProvisioner</c> makes uses it, and so does
    /// `scripts/keycloak-seed.sh`'s own `api()` helper
    /// (`$KEYCLOAK_URI/admin/realms/$path`).
    /// </summary>
    private const string KeycloakAdminApiPathFragment = "/admin/realms/";

    /// <summary>
    /// Restates, by name, a guarantee <see cref="OnionDependencyRules.Layer_references_only_allowed_projects"/>
    /// already enforces as a side effect: <c>Mustask.AppHost</c> is not in any
    /// layer's allowed-reference list, so a <c>ProjectReference</c> to it
    /// from any shipping project already fails that test today. Kept as its
    /// own fact so the connection to this guarantee is not left
    /// implicit in a test that predates it.
    /// </summary>
    [Theory]
    [InlineData("Mustask.Shared")]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Infrastructure")]
    [InlineData("Mustask.Api")]
    public void ShippingProject_DoesNotReferenceAppHost(string project)
    {
        var references = RepositoryLayout.ProjectReferencesOf(project);

        Assert.DoesNotContain("Mustask.AppHost", references);
    }

    /// <summary>
    /// Catches a <em>reference</em> to the provisioning code — an accidental
    /// `using Mustask.AppHost.Provisioning`, or a copy that still names where it
    /// came from. A backend check found this test's own docstring
    /// had overclaimed what it verifies: a scan for this literal string
    /// cannot see a semantically equivalent reimplementation under an
    /// unrelated name that never mentions the namespace at all — that is
    /// what <see cref="ShippingProject_DoesNotCallKeycloaksAdminApi"/> exists
    /// for, as a second, independent check, not a replacement.
    /// </summary>
    [Theory]
    [InlineData("Mustask.Shared")]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Infrastructure")]
    [InlineData("Mustask.Api")]
    public void ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace(string project)
    {
        var offenders = RepositoryLayout.SourceFilesOf(project)
            .Where(file => File.ReadAllText(file.FullName).Contains(ProvisioningNamespace, StringComparison.Ordinal))
            .Select(file => file.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{project} упоминает `{ProvisioningNamespace}` в: {string.Join(", ", offenders)}. "
            + "Код, создающий стендовый клиент с правом имперсонации, живёт только в "
            + "Mustask.AppHost — ссылка или копия этого кода в проекте, "
            + "который куда-то поставляется, ровно то, что этот тест обязан ловить.");
    }

    /// <summary>
    /// Guards the <em>capability</em>, not a
    /// name — a shipping project has no legitimate reason to call Keycloak's
    /// admin REST API at all (verified above, in this class's own summary,
    /// not assumed), so any source that does — under any class name, in any
    /// namespace, whether or not it ever mentions
    /// <c>Mustask.AppHost.Provisioning</c> — is doing the thing ADR-0023 forbids.
    /// This is exactly the invariant a backend check's reproduction (a class
    /// `SneakyKeycloakClientCreator` reimplementing the client-creation POST
    /// under an unrelated name) cannot dodge while still doing what it set
    /// out to do: it still has to call an endpoint under
    /// <c>/admin/realms/...</c>, because that is the only place Keycloak's
    /// admin API lives.
    /// </summary>
    [Theory]
    [InlineData("Mustask.Shared")]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Infrastructure")]
    [InlineData("Mustask.Api")]
    public void ShippingProject_DoesNotCallKeycloaksAdminApi(string project)
    {
        var offenders = RepositoryLayout.SourceFilesOf(project)
            .Where(file => File.ReadAllText(file.FullName).Contains(KeycloakAdminApiPathFragment, StringComparison.Ordinal))
            .Select(file => file.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{project} упоминает `{KeycloakAdminApiPathFragment}` в: {string.Join(", ", offenders)}. "
            + $"{project} — resource server/внутренний слой, не потребитель admin REST API Keycloak "
            + "(этот проект читает только discovery/JWKS, token endpoint и end_session_endpoint). "
            + "Любой код, который обращается к admin-API, независимо от имени класса и namespace, "
            + "делает ровно то, что запрещает ADR-0023: право заводить клиентов и "
            + "пользователей realm'а не должно жить нигде, кроме Mustask.AppHost.");
    }

    /// <summary>
    /// Not-vacuous guard, same idiom as <see cref="RulesAreNotVacuous"/>: the
    /// scan above is only meaningful if the type it looks for actually
    /// exists where expected. A rename or a move to a different namespace
    /// must fail this test loudly, not make the scan above pass because it
    /// no longer finds anything to match.
    /// </summary>
    [Fact]
    public void StandOnlyProvisioner_ExistsInAppHost_UnderTheExpectedNamespace()
    {
        var files = RepositoryLayout.SourceFilesOf("Mustask.AppHost").ToList();

        Assert.True(
            files.Count > 0,
            "В Mustask.AppHost не найдено ни одного .cs файла — проверки этого класса ничего не проверяют.");

        // A plain Contains("class KeycloakStandProvisioner") would also match
        // "class KeycloakStandProvisionerFoo" — exactly the silent-rename
        // case this guard exists to catch — so the class name is matched on
        // a word boundary, not as a substring.
        var declaresClass = new Regex(@"\bclass\s+KeycloakStandProvisioner\b");

        var declaresProvisioner = files.Any(file =>
        {
            var text = File.ReadAllText(file.FullName);
            return text.Contains($"namespace {ProvisioningNamespace}", StringComparison.Ordinal)
                && declaresClass.IsMatch(text);
        });

        Assert.True(
            declaresProvisioner,
            $"Не нашёл `class KeycloakStandProvisioner` в namespace `{ProvisioningNamespace}` внутри "
            + "Mustask.AppHost. Если класс переехал или переименован — обнови и эту проверку, и "
            + "ShippingProject_DoesNotUseTheStandOnlyProvisioningNamespace, иначе она "
            + "молча перестанет что-либо ловить.");
    }
}
