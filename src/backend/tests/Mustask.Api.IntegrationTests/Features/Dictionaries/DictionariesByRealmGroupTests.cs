using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// mod-016 US-4 · "Instance codes still come from the token" and US-8 · "Every signed-in user reads the dictionaries" (BR-M1-29): the
/// dictionaries are guarded by instance codes from the token, and the token of each kind of user is built here from the
/// <b>repository's real realm file</b> — what Keycloak puts in <c>resource_access.mustask-api.roles</c> for a user in no group, in
/// «Создатели организаций» only, in «Администраторы справочников» and in «Администраторы» (the realm's default roles plus the roles
/// of the user's group) — so a realm edit that moves a right and the API's guards are tested together. Everyone reads; only the
/// dictionaries administrator and the instance administrator write; and none of the codes opens an organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DictionariesByRealmGroupTests(MustaskApiFactory factory)
{
    private const string Languages = "/api/dictionaries/languages";

    /// <summary>The <c>mustask-api</c> codes the realm gives a user in <paramref name="groupPath"/> (or in none): the default role and the group's roles.</summary>
    private static string[] CodesOf(string? groupPath)
    {
        var path = Path.Combine(RepositoryRoot(), "deploy", "keycloak", "realms", "mustask-realm.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var realm = document.RootElement;
        var roles = realm.GetProperty("roles").GetProperty("realm").EnumerateArray().ToDictionary(role => role.GetProperty("name").GetString()!, role => role.Clone());

        var held = new List<string> { realm.GetProperty("defaultRole").GetProperty("name").GetString()! };

        if (groupPath is not null)
        {
            var group = realm.GetProperty("groups").EnumerateArray().Single(candidate => "/" + candidate.GetProperty("name").GetString() == groupPath);
            held.AddRange(group.GetProperty("realmRoles").EnumerateArray().Select(role => role.GetString()!));
        }

        return held
            .SelectMany(name => roles[name].TryGetProperty("composites", out var composites) && composites.TryGetProperty("client", out var clients) && clients.TryGetProperty("mustask-api", out var codes)
                ? codes.EnumerateArray().Select(code => code.GetString()!)
                : [])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Mustask.sln not found above the test binaries.");
    }

    private async Task<(HttpStatusCode Read, HttpStatusCode Write)> ReadAndWriteAsync(string[] codes)
    {
        var token = KeycloakTestRealm.CreateToken(codes, options => options.Subject = OrganizationsTokens.NewSubject());

        var read = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Languages, token: token);
        var write = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, Languages, new { }, token);

        return (read.Status, write.Status);
    }

    public static TheoryData<string?, bool> Users() => new()
    {
        { null, false },
        { "/Создатели организаций", false },
        { "/Администраторы справочников", true },
        { "/Администраторы", true },
    };

    // US-8 · "A user in no group, a member of /Создатели организаций only, and a member of /Администраторы справочников: each reads the
    // languages list (BR-M1-29); only the dictionaries administrator and an instance administrator write, and a write by anyone else is
    // refused" — with the codes the real realm gives each of them.
    [Theory]
    [MemberData(nameof(Users))]
    public async Task EveryoneReadsTheLanguages_OnlyTheAdministratorsWrite(string? groupPath, bool mayWrite)
    {
        var codes = CodesOf(groupPath);

        var (read, write) = await ReadAndWriteAsync(codes);

        Assert.Equal(HttpStatusCode.OK, read);

        if (mayWrite)
        {
            // Past the guard: the empty body is refused by the validator, not by authorization.
            Assert.NotEqual(HttpStatusCode.Forbidden, write);
            Assert.NotEqual(HttpStatusCode.Unauthorized, write);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, write);
        }
    }

    // The realm gives a user in no group the read and nothing else; the groups add exactly their codes.
    [Fact]
    public void TheRealmGivesAUserInNoGroup_TheReadCodeOnly()
    {
        Assert.Equal(["dictionaries.languages.read"], CodesOf(null));
        Assert.Equal(["dictionaries.languages.read", "organizations.organization.create"], CodesOf("/Создатели организаций"));
        Assert.Equal(["dictionaries.languages.read", "dictionaries.languages.write"], CodesOf("/Администраторы справочников"));
        Assert.Equal(
            ["dictionaries.languages.read", "dictionaries.languages.write", "organizations.organization.create"],
            CodesOf("/Администраторы"));
    }

    // US-4 · none of those codes opens an organization: an instance administrator, with every code the realm can give, and no
    // membership, gets 404 on an organization and an empty list of organizations — the codes are instance rights, membership decides
    // the rest.
    [Fact]
    public async Task TheInstanceAdministratorsCodes_OpenNoOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var token = KeycloakTestRealm.CreateToken(CodesOf("/Администраторы"), options => options.Subject = OrganizationsTokens.NewSubject());

        var opened = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: token);
        var listed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, "/api/organizations", token: token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (opened.Status, opened.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(0, listed.Body.GetArrayLength());
    }
}
