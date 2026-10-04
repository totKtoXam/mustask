namespace Mustask.Architecture.Tests;

/// <summary>
/// mod-014 (ADR-0025 § 9; BR-UP-1, BR-UP-9, BR-UP-12): the boundaries of the
/// user projection, checked by source like <see cref="SessionIsolationRules"/>.
/// <list type="bullet">
/// <item>US-1 "Reads work while Keycloak is unreachable": nothing of the
/// sub-slice holds an <c>HttpClient</c> or names Keycloak's admin surface —
/// MUSTASK never calls Keycloak for a user (BR-UP-9).</item>
/// <item>US-6 "A sign-in does not fill the projection": the write side and the
/// storage — the stores <c>IProjectedUserStore</c> and
/// <c>IDirectorySnapshotStore</c>, the entities <c>ProjectedUser</c> and
/// <c>DirectorySnapshot*</c>, and <c>MustaskDbContext</c>'s sets of them — are
/// used only by the sub-slice's own folders, the context, its migrations and
/// the DI wiring (BR-UP-1; api-contract.md A.8: consumers never read the
/// projection's tables, EF entities or stores).</item>
/// <item>US-3 "The status authorizes nothing": authentication and
/// authorization never consult the read model (BR-UP-12).</item>
/// </list>
/// </summary>
public sealed class UserDirectoryBoundaryRules
{
    private static readonly string[] Layers = ["Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api"];

    /// <summary>
    /// The projection's internals, matched as text: the store interfaces, the entities (<c>ProjectedUser</c> also
    /// matches its store and lifecycle; <c>DirectorySnapshot</c> every snapshot type, set and store) and the
    /// context's set of users. Consumers read <c>IUserDirectory</c> and <c>DirectoryUser</c>, which match none.
    /// </summary>
    private static readonly string[] ProjectionInternals =
        ["IProjectedUserStore", "IDirectorySnapshotStore", "ProjectedUser", "DirectorySnapshot", "DirectoryUsers"];

    /// <summary>Where the internals may appear: the sub-slice, the context that maps them, its migrations, DI.</summary>
    private static readonly string[] InternalsAllowedIn =
    [
        "Mustask.Domain/UserDirectory/",
        "Mustask.Application/Abstractions/UserDirectory/",
        "Mustask.Application/Features/UserDirectory/",
        "Mustask.Infrastructure/UserDirectory/",
        "Mustask.Infrastructure/DependencyInjection.cs",
        "Mustask.Infrastructure/MustaskDbContext.cs",
        "Mustask.Infrastructure/Migrations/",
    ];

    [Fact]
    public void UserDirectory_sources_hold_no_http_client_and_no_keycloak_admin_call()
    {
        string[] forbidden = ["HttpClient", "System.Net.Http", "/admin/realms", "Keycloak.AuthServices"];

        var violations = UserDirectoryFiles()
            .SelectMany(file => File.ReadLines(file.FullName)
                .Where(line => !IsComment(line))
                .Where(line => forbidden.Any(token => line.Contains(token, StringComparison.Ordinal)))
                .Select(line => $"{RelativePath(file)}: {line.Trim()}"))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "The user directory reaches out over HTTP — its reads must answer from MUSTASK's own database (BR-UP-9):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Projection_write_side_and_storage_are_used_only_by_the_sub_slice()
    {
        var violations = InternalsViolations(
            Layers.SelectMany(RepositoryLayout.SourceFilesOf).Select(file => (RelativePath(file), File.ReadLines(file.FullName))));

        Assert.True(
            violations.Count == 0,
            "Code outside the user directory's sub-slice uses the projection's stores, entities or tables — the provider is "
            + "its only writer (BR-UP-1) and consumers read IUserDirectory only (api-contract.md A.8):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Code review F10: the rule above cannot pass vacuously — every internal it names exists where it is allowed
    /// (a rename would otherwise leave it matching nothing), and a use outside the sub-slice is flagged, for each kind:
    /// a consumer's store on the context's set, a consumer's handler on the entity, one on the snapshot entity.
    /// </summary>
    [Fact]
    public void Projection_internals_rule_is_not_vacuous()
    {
        var sources = Layers.SelectMany(RepositoryLayout.SourceFilesOf).ToList();

        foreach (var token in ProjectionInternals)
        {
            Assert.True(
                sources.Any(file => File.ReadLines(file.FullName).Any(line => !IsComment(line) && line.Contains(token, StringComparison.Ordinal))),
                $"No source names `{token}` — the internals rule checks for something that no longer exists.");
        }

        var flagged = InternalsViolations(
        [
            ("Mustask.Infrastructure/Organizations/MemberStore.cs", ["var active = context.DirectoryUsers.Where(user => user.IsActive);"]),
            ("Mustask.Application/Features/Organizations/AddMember/AddMemberHandler.cs", ["ProjectedUser user = await load(id);"]),
            ("Mustask.Api/Features/Organizations/OrganizationsEndpoints.cs", ["var snapshot = new DirectorySnapshot();"]),
            ("Mustask.Infrastructure/MustaskDbContext.cs", ["public DbSet<ProjectedUser> DirectoryUsers => Set<ProjectedUser>();"]),
            ("Mustask.Infrastructure/Organizations/Comments.cs", ["// reads context.DirectoryUsers"]),
        ]);

        string[] expected =
        [
            "Mustask.Infrastructure/Organizations/MemberStore.cs: DirectoryUsers",
            "Mustask.Application/Features/Organizations/AddMember/AddMemberHandler.cs: ProjectedUser",
            "Mustask.Api/Features/Organizations/OrganizationsEndpoints.cs: DirectorySnapshot",
        ];

        Assert.Equal(expected, flagged);
    }

    private static List<string> InternalsViolations(IEnumerable<(string Path, IEnumerable<string> Lines)> files) =>
        files
            .Where(file => !InternalsAllowedIn.Any(prefix => file.Path.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(file => file.Lines
                .Where(line => !IsComment(line))
                .SelectMany(line => ProjectionInternals.Where(token => line.Contains(token, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Select(token => $"{file.Path}: {token}"))
            .ToList();

    [Fact]
    public void Authentication_and_authorization_do_not_consult_the_read_model()
    {
        string[] guarded = ["Mustask.Api/Authentication/", "Mustask.Api/Authorization/", "Mustask.Api/CurrentUser/"];
        string[] forbidden = ["Abstractions.UserDirectory", "IUserDirectory", "DirectoryUser", "Domain.UserDirectory"];

        var guardedFiles = RepositoryLayout.SourceFilesOf("Mustask.Api")
            .Where(file => guarded.Any(prefix => RelativePath(file).StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.NotEmpty(guardedFiles);

        var violations = guardedFiles
            .Where(file => File.ReadLines(file.FullName)
                .Any(line => !IsComment(line) && forbidden.Any(token => line.Contains(token, StringComparison.Ordinal))))
            .Select(RelativePath)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Authentication or authorization reads the user projection — an inactive status authorizes nothing (BR-UP-12): "
            + string.Join(", ", violations));
    }

    /// <summary><see cref="RulesAreNotVacuous"/>'s idea for this file: an empty selection would make every rule above pass.</summary>
    [Fact]
    public void UserDirectory_files_are_discovered_in_every_layer()
    {
        foreach (var layer in Layers)
        {
            Assert.True(
                UserDirectoryFiles().Any(file => RelativePath(file).StartsWith(layer + "/", StringComparison.Ordinal)),
                $"No UserDirectory source found in {layer} — UserDirectoryBoundaryRules checks nothing there.");
        }

        Assert.Contains(
            Layers.SelectMany(RepositoryLayout.SourceFilesOf),
            file => File.ReadAllText(file.FullName).Contains("IProjectedUserStore", StringComparison.Ordinal)
                && RelativePath(file).StartsWith("Mustask.Application/Features/UserDirectory/", StringComparison.Ordinal));
    }

    private static IEnumerable<FileInfo> UserDirectoryFiles() =>
        Layers
            .SelectMany(RepositoryLayout.SourceFilesOf)
            .Where(file => RelativePath(file).Contains("/UserDirectory/", StringComparison.Ordinal));

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*');
    }

    private static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(RepositoryLayout.Source.FullName, file.FullName).Replace('\\', '/');
}
