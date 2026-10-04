using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// What a realm-import file of <c>deploy/keycloak/realms</c> declares, for
/// the stand's drift check (ADR-0025 § 7): realm roles and their composites,
/// the declared clients with their roles, URLs and whether they carry a
/// secret, groups by path with their role mappings, and the event listeners.
/// Keycloak's <c>${NAME:default}</c> placeholders are resolved with the
/// stand's own values, the way Keycloak resolved them when the store was
/// created.
/// </summary>
public sealed partial record KeycloakRealmImport(
    string Realm,
    IReadOnlyDictionary<string, DeclaredRole> RealmRoles,
    IReadOnlyDictionary<string, DeclaredClient> Clients,
    IReadOnlyList<DeclaredGroup> Groups,
    IReadOnlyList<string> EventsListeners)
{
    /// <summary>Reads the import file of <paramref name="realm"/> among the <c>*.json</c> files of <paramref name="realmsDirectory"/>.</summary>
    public static KeycloakRealmImport Load(string realmsDirectory, string realm, IReadOnlyDictionary<string, string> placeholders)
    {
        foreach (var file in Directory.EnumerateFiles(realmsDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            var json = File.ReadAllText(file);

            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

            if (document.RootElement.TryGetProperty("realm", out var name) && name.GetString() == realm)
            {
                return Parse(json, placeholders);
            }
        }

        throw new InvalidOperationException($"No realm-import file in {realmsDirectory} declares realm `{realm}`.");
    }

    public static KeycloakRealmImport Parse(string json, IReadOnlyDictionary<string, string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(placeholders);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = document.RootElement;
        string Resolve(string value) => Placeholder().Replace(value, match =>
            placeholders.TryGetValue(match.Groups["name"].Value, out var resolved) ? resolved
            : match.Groups["default"].Success ? match.Groups["default"].Value
            : match.Value);

        var realmRoles = new Dictionary<string, DeclaredRole>(StringComparer.Ordinal);
        var clientRoles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        if (root.TryGetProperty("roles", out var roles))
        {
            foreach (var role in Array(roles, "realm"))
            {
                var name = role.GetProperty("name").GetString()!;
                var realmComposites = new HashSet<string>(StringComparer.Ordinal);
                var clientComposites = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

                if (role.TryGetProperty("composites", out var composites))
                {
                    realmComposites.UnionWith(Strings(composites, "realm"));

                    if (composites.TryGetProperty("client", out var byClient))
                    {
                        foreach (var client in byClient.EnumerateObject())
                        {
                            clientComposites[client.Name] = client.Value.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
                        }
                    }
                }

                realmRoles[name] = new DeclaredRole(name, realmComposites, clientComposites);
            }

            if (roles.TryGetProperty("client", out var byClientRoles))
            {
                foreach (var client in byClientRoles.EnumerateObject())
                {
                    clientRoles[client.Name] = client.Value.EnumerateArray()
                        .Select(role => role.GetProperty("name").GetString()!)
                        .ToHashSet(StringComparer.Ordinal);
                }
            }
        }

        var clients = new Dictionary<string, DeclaredClient>(StringComparer.Ordinal);

        foreach (var client in Array(root, "clients"))
        {
            var clientId = client.GetProperty("clientId").GetString()!;
            string? postLogout = null;

            if (client.TryGetProperty("attributes", out var attributes)
                && attributes.TryGetProperty("post.logout.redirect.uris", out var postLogoutValue))
            {
                postLogout = Resolve(postLogoutValue.GetString()!);
            }

            clients[clientId] = new DeclaredClient(
                clientId,
                clientRoles.TryGetValue(clientId, out var declaredRoles) ? declaredRoles : new HashSet<string>(StringComparer.Ordinal),
                client.TryGetProperty("rootUrl", out var rootUrl) ? Resolve(rootUrl.GetString()!) : null,
                Strings(client, "redirectUris").Select(Resolve).ToHashSet(StringComparer.Ordinal),
                postLogout,
                client.TryGetProperty("secret", out _));
        }

        var groups = new List<DeclaredGroup>();

        foreach (var group in Array(root, "groups"))
        {
            AddGroup(groups, group, parentPath: string.Empty);
        }

        return new KeycloakRealmImport(
            root.GetProperty("realm").GetString()!,
            realmRoles,
            clients,
            groups,
            Strings(root, "eventsListeners").ToList());
    }

    private static void AddGroup(List<DeclaredGroup> groups, JsonElement group, string parentPath)
    {
        var path = parentPath + "/" + group.GetProperty("name").GetString();
        var clientRoles = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        if (group.TryGetProperty("clientRoles", out var byClient))
        {
            foreach (var client in byClient.EnumerateObject())
            {
                clientRoles[client.Name] = client.Value.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
            }
        }

        groups.Add(new DeclaredGroup(path, Strings(group, "realmRoles").ToHashSet(StringComparer.Ordinal), clientRoles));

        foreach (var child in Array(group, "subGroups"))
        {
            AddGroup(groups, child, path);
        }
    }

    private static IEnumerable<JsonElement> Array(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().ToList()
            : [];

    private static IEnumerable<string> Strings(JsonElement element, string property) =>
        Array(element, property).Select(item => item.GetString()!);

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z0-9_]+)(:(?<default>[^}]*))?\}")]
    private static partial Regex Placeholder();
}

/// <summary>A realm role and its composites: realm roles, and client roles by client id.</summary>
public sealed record DeclaredRole(
    string Name,
    IReadOnlySet<string> RealmComposites,
    IReadOnlyDictionary<string, IReadOnlySet<string>> ClientComposites);

/// <summary>A client the import declares: its roles, the URLs filled from the stand's ports, and whether it has a secret.</summary>
public sealed record DeclaredClient(
    string ClientId,
    IReadOnlySet<string> Roles,
    string? RootUrl,
    IReadOnlySet<string> RedirectUris,
    string? PostLogoutRedirectUris,
    bool HasSecret);

/// <summary>A group by its path (<c>/Parent/Child</c>) with its realm and client role mappings.</summary>
public sealed record DeclaredGroup(
    string Path,
    IReadOnlySet<string> RealmRoles,
    IReadOnlyDictionary<string, IReadOnlySet<string>> ClientRoles);
