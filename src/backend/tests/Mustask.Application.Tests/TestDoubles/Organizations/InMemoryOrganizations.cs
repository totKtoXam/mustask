using System.Globalization;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// The one in-memory store behind every organizations fake, so a handler that
/// reads through one abstraction and writes through another sees one state.
/// It records every lock and every save in <see cref="Calls"/>, so a test can
/// pin the order of answers and the lock order (plan-backend § 5.3, § 8.2),
/// and it can make the next save fail the way the loser of a race does
/// (<see cref="FailNextSave"/>).
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

    public List<Area> Areas { get; } = [];

    /// <summary>
    /// Every lock and save, in order: <c>lock organization {id}</c>, <c>lock project {id}</c>,
    /// <c>lock team {mode} {id}</c>, <c>lock area {mode} {id}</c>, <c>lock root {mode} {projectId}</c>,
    /// <c>lock default {teamId}</c>, <c>reassign {from} {to}</c>, <c>save</c>.
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

    public NewProject AddProject(Organization organization, string name = "Пример проекта", string key = "PRM")
    {
        var project = Project.Create(organization.Id, Name(name), key, Now, Creator);
        Store(project);

        return project;
    }

    public NewTeam AddTeam(NewProject project, string name = "Платформа")
    {
        var team = Team.CreateWithArea(project.Project.Id, Name(name), project.Root);
        Store(team);

        return team;
    }

    public Area AddArea(Area parent, string name, Team? owner = null)
    {
        var area = Area.CreateChild(parent, Name(name), owner);
        Areas.Add(area);

        return area;
    }

    public void Store(NewProject project)
    {
        Projects.Add(project.Project);
        Teams.Add(project.FirstTeam);
        Areas.Add(project.Root);
    }

    public void Store(NewTeam team)
    {
        Teams.Add(team.Team);
        Areas.Add(team.Area);
    }

    public Organization? FindOrganization(Guid id) => Organizations.SingleOrDefault(organization => organization.Id == id);

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
    internal void Unstore(Area area) => Areas.Remove(area);

    internal Task SaveAsync()
    {
        Record("save");

        if (_nextSaveFailure is { } failure)
        {
            _nextSaveFailure = null;
            _pending.Clear();
            throw failure;
        }

        foreach (var entity in _pending)
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
                case Area area:
                    Areas.Add(area);
                    break;
                default:
                    throw new InvalidOperationException($"The store keeps no {entity.GetType().Name}.");
            }
        }

        _pending.Clear();

        return Task.CompletedTask;
    }
}
