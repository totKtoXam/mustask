using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>Builders for the organizations domain tests: names, a project with its team and root, a second team.</summary>
internal static class OrganizationsDomainTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public const string Creator = "6f1c2d3e-0000-4000-8000-00000000c0de";

    public static EntityName Name(string value) =>
        EntityName.TryCreate(value, out var name) ? name : throw new ArgumentException($"«{value}» is not a valid name.", nameof(value));

    public static Organization AnOrganization(string name = "Пример организации") =>
        Organization.Create(Guid.CreateVersion7(), Name(name), ProcessId.New(), Now, Creator);

    public static NewProject AProject(string name = "Пример проекта", string key = "PRM") =>
        Project.Create(Guid.CreateVersion7(), Name(name), key, Now, Creator);

    public static Team ATeam(Guid organizationId, string name = "Платформа") =>
        Team.Create(organizationId, Name(name));

    public static AreaNode Node(Area area) => new(area.Id, area.ParentId, area.Name);

    public static AreaTree Tree(params Area[] areas) => new(areas.Select(Node));
}
