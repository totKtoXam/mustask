using System.Globalization;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// The one in-memory store behind every organizations fake, so a handler that
/// reads through one abstraction and writes through another sees one state.
/// It records every lock and every save in <see cref="Calls"/>, so a test can
/// pin the order of answers and the lock order (plan-backend § 5.3, § 8.2),
/// and it can make the next save fail the way the loser of a race does
/// (<see cref="FailNextSave"/>). mod-016 adds the memberships and the database
/// rules around them that a handler leans on: the primary keys and the
/// composite key from a team membership to the organization membership.
/// </summary>
public sealed class InMemoryOrganizations
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public const string Creator = "5d1e8f3a-0000-4000-8000-00000000c0de";

    /// <summary>Display order as the database gives it: Russian, ignoring letter case, then by id.</summary>
    public static readonly StringComparer DisplayOrder = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true);

    public List<Organization> Organizations { get; } = [];

    public List<Project> Projects { get; } = [];

    public List<Team> Teams { get; } = [];

    /// <summary>The project–team links (feat-028 BR-OL-4): one per pair, with who made it and when.</summary>
    public List<ProjectTeam> Links { get; } = [];

    public List<Area> Areas { get; } = [];

    /// <summary>The area–team links (mod-018 BR-AW-6).</summary>
    public List<AreaTeam> AreaTeams { get; } = [];

    public List<OrganizationMembership> Memberships { get; } = [];

    public List<TeamMembership> TeamMemberships { get; } = [];

    /// <summary>
    /// Every lock and save, in order: <c>lock organization {id}</c>, <c>lock project {id}</c>,
    /// <c>lock team {mode} {id}</c>, <c>lock project teams {projectId}</c>, <c>lock area {mode} {id}</c>,
    /// <c>lock area tree {organizationId}</c>, <c>clear default team {teamId}</c>, <c>lock member {userId}</c>,
    /// <c>delete member {userId}</c>, <c>end team memberships {teamId}</c>, <c>save</c>; the work items' and the
    /// boards' calls of a team deletion (<see cref="FakeWorkItemTeamOwnership"/>) join the same list.
    /// </summary>
    public List<string> Calls { get; } = [];

    public int SaveCount => Calls.Count(call => call == "save");

    private readonly List<object> _pending = [];

    private Exception? _nextSaveFailure;

    /// <summary>
    /// The next save throws <paramref name="failure"/> and stores nothing it was
    /// given since the last save, as the loser of a race hears its 409.
    /// </summary>
    public void FailNextSave(Exception failure) => _nextSaveFailure = failure;

    public static EntityName Name(string value) =>
        EntityName.TryCreate(value, out var name) ? name : throw new ArgumentException($"«{value}» is not a valid name.", nameof(value));

    public Organization AddOrganization(string name = "Пример организации")
    {
        var organization = Organization.Create(Guid.CreateVersion7(), Name(name), ProcessId.New(), Now, Creator);
        Organizations.Add(organization);

        return organization;
    }

    /// <summary>An organization that <paramref name="userId"/> belongs to in <paramref name="role"/> (the list handlers narrow by membership).</summary>
    public Organization AddOrganizationOf(string userId, string name = "Пример организации", OrganizationRole role = OrganizationRole.Admin)
    {
        var organization = AddOrganization(name);
        AddMember(organization, userId, role);

        return organization;
    }

    /// <summary>A membership of <paramref name="userId"/> in <paramref name="organization"/>, added by the organization's creator.</summary>
    public OrganizationMembership AddMember(Organization organization, string userId, OrganizationRole role = OrganizationRole.Member)
    {
        var membership = OrganizationMembership.Add(organization.Id, userId, role, Creator, Now);
        Memberships.Add(membership);

        return membership;
    }

    /// <summary>A team membership, as the database would hold it: the user must be a member of the organization.</summary>
    public TeamMembership AddTeamMember(Organization organization, Team team, string userId)
    {
        var membership = TeamMembership.Add(team.Id, organization.Id, userId);
        TeamMemberships.Add(membership);

        return membership;
    }

    /// <summary>A project with its node (mod-018 OQ-5 A): at the top of the tree, or under <paramref name="department"/>.</summary>
    public NewProject AddProject(Organization organization, string name = "Пример проекта", string key = "PRM", Area? department = null)
    {
        var created = Project.Create(organization.Id, Name(name), key, Now, Creator, parentAreaId: department?.Id);
        Store(created);

        return created;
    }

    public void Store(NewProject project)
    {
        Projects.Add(project.Project);
        Areas.Add(project.Root);
    }

    /// <summary>The project's node in the area tree.</summary>
    public Area RootOf(Project project) => Areas.Single(area => area.ProjectId == project.Id && area.Type == AreaType.Project);

    /// <summary>A team of the organization (feat-028 BR-OL-1): linked to no project until <see cref="Link"/> says so.</summary>
    public Team AddTeam(Organization organization, string name = "Платформа")
    {
        var team = Team.Create(organization.Id, Name(name));
        Teams.Add(team);

        return team;
    }

    /// <summary>A link between the project and the team, made by <see cref="Creator"/> at <see cref="Now"/>.</summary>
    public ProjectTeam Link(NewProject project, Team team)
    {
        var link = ProjectTeam.Link(project.Project.OrganizationId, project.Project.Id, team.Id, Creator, Now);
        Links.Add(link);

        return link;
    }

    public ProjectTeam Link(Project project, Team team)
    {
        var link = ProjectTeam.Link(project.OrganizationId, project.Id, team.Id, Creator, Now);
        Links.Add(link);

        return link;
    }

    public Team AddLinkedTeam(Organization organization, Project project, string name = "Платформа")
    {
        var team = AddTeam(organization, name);
        Link(project, team);

        return team;
    }

    /// <summary>A team of the project's organization, linked to the project.</summary>
    public Team AddLinkedTeam(Organization organization, NewProject project, string name = "Платформа")
    {
        var team = AddTeam(organization, name);
        Link(project, team);

        return team;
    }

    public bool IsLinked(Guid projectId, Guid teamId) => Links.Any(link => link.ProjectId == projectId && link.TeamId == teamId);

    /// <summary>A department at the top of the organization's tree (mod-018 BR-AW-3).</summary>
    public Area AddDepartment(Organization organization, string name)
    {
        var area = Area.CreateDepartment(organization.Id, Name(name), null, Creator, Now);
        Areas.Add(area);

        return area;
    }

    /// <summary>A sub-area below a project node or another sub-area (OQ-7 A).</summary>
    public Area AddArea(Area parent, string name)
    {
        var area = Area.CreateSubArea(parent, Name(name), null, Creator, Now);
        Areas.Add(area);

        return area;
    }

    public AreaTeam LinkArea(Area area, Team team)
    {
        var link = AreaTeam.Link(area.OrganizationId, area.Id, team.Id, Creator, Now);
        AreaTeams.Add(link);

        return link;
    }

    public Organization? FindOrganization(Guid id) => Organizations.SingleOrDefault(organization => organization.Id == id);

    public OrganizationMembership? FindMembership(Guid organizationId, string userId) =>
        Memberships.SingleOrDefault(membership => membership.OrganizationId == organizationId && membership.UserId == userId);

    public Project? FindProject(Guid organizationId, Guid projectId) =>
        Projects.SingleOrDefault(project => project.Id == projectId && project.OrganizationId == organizationId);

    /// <summary>ADR-0026 rule 3: the project is active, in that organization, and the organization is active.</summary>
    public bool IsReachable(Guid organizationId, Guid projectId) =>
        FindOrganization(organizationId) is { IsActive: true }
        && FindProject(organizationId, projectId) is { IsActive: true };

    public IEnumerable<T> InDisplayOrder<T>(IEnumerable<T> items, Func<T, string> name, Func<T, Guid> id) =>
        items.OrderBy(name, DisplayOrder).ThenBy(id);

    internal void Record(string call) => Calls.Add(call);

    /// <summary>A repository's <c>Add</c>: stored at the next save, as a tracked entity is written at the next flush.</summary>
    internal void Stage(object entity) => _pending.Add(entity);

    /// <summary>A repository's <c>Remove</c>: gone at once (the fakes do not model a deferred delete).</summary>
    internal void Unstore(Area area)
    {
        Areas.Remove(area);
        AreaTeams.RemoveAll(link => link.AreaId == area.Id);
    }

    /// <summary>
    /// The database's <c>DELETE FROM organization_memberships</c> with the cascade of its composite key: the user's team
    /// memberships of that organization go in the same statement (BR-M1-36). Answers whether a membership was deleted.
    /// </summary>
    internal bool DeleteMembership(Guid organizationId, string userId)
    {
        var removed = Memberships.RemoveAll(membership => membership.OrganizationId == organizationId && membership.UserId == userId) > 0;

        if (removed)
        {
            TeamMemberships.RemoveAll(membership => membership.OrganizationId == organizationId && membership.UserId == userId);
        }

        return removed;
    }

    internal Task SaveAsync()
    {
        Record("save");

        if (_nextSaveFailure is { } failure)
        {
            _nextSaveFailure = null;
            _pending.Clear();
            throw failure;
        }

        // A flush is atomic: the entities are checked against the keys first, and a refusal stores none of them.
        var staged = _pending.ToList();
        _pending.Clear();

        if (Refuse(staged) is { } refusal)
        {
            throw refusal;
        }

        foreach (var entity in staged)
        {
            switch (entity)
            {
                case Organization organization:
                    Organizations.Add(organization);
                    break;
                case Project project:
                    Projects.Add(project);
                    break;
                case Team team:
                    Teams.Add(team);
                    break;
                case ProjectTeam link:
                    Links.Add(link);
                    break;
                case Area area:
                    Areas.Add(area);
                    break;
                case AreaTeam link:
                    AreaTeams.Add(link);
                    break;
                case OrganizationMembership membership:
                    Memberships.Add(membership);
                    break;
                case TeamMembership membership:
                    TeamMemberships.Add(membership);
                    break;
                default:
                    throw new InvalidOperationException($"The store keeps no {entity.GetType().Name}.");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The database rules of the memberships, as <c>OrganizationsSaveChanges</c> maps them: a second membership of one user in
    /// one organization (<c>already_member</c>), a second membership of one user in one team (<c>team.member_exists</c>), and a
    /// team membership whose organization membership is not there (<c>team.member_not_in_organization</c>), counting the
    /// memberships staged in the same flush.
    /// </summary>
    private MustaskException? Refuse(IReadOnlyList<object> staged)
    {
        var memberships = Memberships.Concat(staged.OfType<OrganizationMembership>()).ToList();

        foreach (var added in staged.OfType<OrganizationMembership>())
        {
            if (Memberships.Any(existing => existing.OrganizationId == added.OrganizationId && existing.UserId == added.UserId))
            {
                return new ConflictException(OrganizationErrorCodes.MembershipAlreadyMember, ErrorParams.None);
            }
        }

        foreach (var added in staged.OfType<TeamMembership>())
        {
            if (TeamMemberships.Any(existing => existing.TeamId == added.TeamId && existing.UserId == added.UserId))
            {
                return new ConflictException(OrganizationErrorCodes.TeamMemberExists, ErrorParams.None);
            }

            if (!memberships.Any(existing => existing.OrganizationId == added.OrganizationId && existing.UserId == added.UserId))
            {
                return new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization, ErrorParams.None);
            }
        }

        return null;
    }
}
