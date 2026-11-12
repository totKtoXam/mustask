using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>
/// The arrangement of an endpoint test, composed from what the test needs and nothing else (tech-035):
/// <code>
/// var world = await World.With(factory).Project().Member("vera", OrganizationRole.Member).BuildAsync();
/// var answer = await world.Admin.PutAsync($"{OrganizationsApi.Organization(world.Organization.Id)}/name", body);
/// var asVera = await world.As("vera").GetAsync(path);
/// </code>
/// It builds through the domain and the repositories (<see cref="OrganizationsTestData"/>, <see cref="MembershipTestData"/>), not
/// through endpoints, so no test depends on another use case. The roles: <see cref="Admin"/> (the founder),
/// <see cref="As(string)"/> (a named member of any organization role), <see cref="Outsider"/> (belongs to nothing),
/// <see cref="SystemAdmin"/> (an instance role, no membership) and <see cref="Anonymous"/>.
/// </summary>
public sealed class World
{
    private readonly MustaskApiFactory _factory;
    private readonly Dictionary<string, MembershipTestData.TestMember> _members;

    internal World(
        MustaskApiFactory factory, Organization organization, ProjectWithTeam? project, Dictionary<string, MembershipTestData.TestMember> members)
    {
        _factory = factory;
        Organization = organization;
        Project = project;
        _members = members;
    }

    public static WorldBuilder With(MustaskApiFactory factory) => new(factory);

    public Organization Organization { get; }

    /// <summary>The project, its root and its one team (<see cref="OrganizationsTestData.CreateProjectAsync"/>); <c>null</c> unless asked for.</summary>
    public ProjectWithTeam? Project { get; }

    /// <summary>The organization's founder and administrator.</summary>
    public ApiClient Admin => new(_factory);

    public ApiClient Anonymous => new ApiClient(_factory).Anonymous();

    /// <summary>A signed-in user nobody has added to anything — a new one on every call.</summary>
    public ApiClient Outsider => new ApiClient(_factory).As(OrganizationsTokens.Stranger());

    /// <summary>An instance administrator with no membership.</summary>
    public ApiClient SystemAdmin => new ApiClient(_factory).As(OrganizationsTokens.SystemAdmin());

    /// <summary>A member added with <see cref="WorldBuilder.Member"/>, as a client.</summary>
    public ApiClient As(string alias) => new ApiClient(_factory).As(Member(alias).Token);

    /// <summary>A member's id and token.</summary>
    public MembershipTestData.TestMember Member(string alias) =>
        _members.TryGetValue(alias, out var member) ? member : throw new KeyNotFoundException($"The world has no member «{alias}».");
}

/// <summary>The steps of a <see cref="World"/>; nothing is written until <see cref="BuildAsync"/>.</summary>
public sealed class WorldBuilder
{
    private readonly MustaskApiFactory _factory;
    private readonly List<(string Alias, OrganizationRole Role)> _members = [];
    private string? _organizationName;
    private OrganizationStatus _status = OrganizationStatus.Active;
    private bool _founded = true;
    private (string Name, string Key)? _project;

    internal WorldBuilder(MustaskApiFactory factory) => _factory = factory;

    /// <summary>
    /// Configures the organization (every world has one): a unique name unless given, active unless <paramref name="status"/> says
    /// otherwise, founded by <see cref="Mustask.Testing.TestData.Creator"/> as its administrator unless <paramref name="founded"/>
    /// is false.
    /// </summary>
    public WorldBuilder Organization(string? name = null, OrganizationStatus status = OrganizationStatus.Active, bool founded = true)
    {
        _organizationName = name;
        _status = status;
        _founded = founded;

        return this;
    }

    /// <summary>A project with its root and one team named after it.</summary>
    public WorldBuilder Project(string name = "Пример проекта", string key = "PRM")
    {
        _project = (name, key);

        return this;
    }

    /// <summary>A member of the organization, a new user, reachable as <c>world.As(alias)</c>.</summary>
    public WorldBuilder Member(string alias, OrganizationRole role)
    {
        if (_members.Exists(member => member.Alias == alias))
        {
            throw new ArgumentException($"The world already has a member «{alias}».", nameof(alias));
        }

        _members.Add((alias, role));

        return this;
    }

    public async Task<World> BuildAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(_factory, _organizationName, _status, _founded);
        var project = _project is { } wanted
            ? await OrganizationsTestData.CreateProjectAsync(_factory, organization.Id, wanted.Name, wanted.Key)
            : null;
        var members = new Dictionary<string, MembershipTestData.TestMember>(StringComparer.Ordinal);

        foreach (var (alias, role) in _members)
        {
            members[alias] = await MembershipTestData.NewMemberAsync(_factory, organization.Id, role);
        }

        return new World(_factory, organization, project, members);
    }
}
