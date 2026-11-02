using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// The state the membership handlers' tests share (mod-016): one organization with a project, its team and a second team
/// «Платформа», a user directory, the acting administrator <see cref="AdminId"/> (projected, active, a member), and every fake
/// the ten slices need, all over one <see cref="InMemoryOrganizations"/>.
/// </summary>
public sealed class MembershipWorld
{
    public const string AdminId = "admin-anna";

    public MembershipWorld()
    {
        Organization = Store.AddOrganization();
        Project = Store.AddProject(Organization).Project;
        Platform = Store.AddLinkedTeam(Organization, Project);
        Clock.UtcNow = new DateTimeOffset(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);
        User.SubjectId = AdminId;
        Members = new FakeMemberReadModel(Store, Directory);
        Memberships = new FakeMembershipRepository(Store);
        TeamMemberships = new FakeTeamMembershipRepository(Store);
        Organizations = new FakeOrganizationRepository(Store);
        Projects = new FakeProjectRepository(Store);
        Teams = new FakeTeamRepository(Store);
        ProjectReads = new FakeProjectReadModel(Store);
        TeamReads = new FakeTeamReadModel(Store);
        AddMember(AdminId, "Анна Смирнова", OrganizationRole.Admin);
    }

    public InMemoryOrganizations Store { get; } = new();

    public FakeUserDirectory Directory { get; } = new();

    public FakeCurrentUser User { get; } = new();

    public FakeClock Clock { get; } = new();

    public Organization Organization { get; }

    public Project Project { get; }

    public Team Platform { get; }

    public FakeMemberReadModel Members { get; }

    public FakeMembershipRepository Memberships { get; }

    public FakeTeamMembershipRepository TeamMemberships { get; }

    public FakeOrganizationRepository Organizations { get; }

    public FakeProjectRepository Projects { get; }

    public FakeTeamRepository Teams { get; }

    public FakeProjectReadModel ProjectReads { get; }

    public FakeTeamReadModel TeamReads { get; }

    public string OrganizationId => Organization.Id.ToString();

    public string ProjectId => Project.Id.ToString();

    public string PlatformId => Platform.Id.ToString();

    /// <summary>A projected user (active unless said otherwise) who is a member of the organization.</summary>
    public string AddMember(string id, string displayName, OrganizationRole role = OrganizationRole.Member, bool isActive = true, string email = "")
    {
        Directory.Add(id, displayName, email, isActive);
        Store.AddMember(Organization, id, role);

        return id;
    }

    /// <summary>A member whom the projection does not know (mod-014 BR-UP-17).</summary>
    public string AddUnknownMember(string id, OrganizationRole role = OrganizationRole.Member)
    {
        Store.AddMember(Organization, id, role);

        return id;
    }

    /// <summary>A projected user who is not a member of anything.</summary>
    public string AddOutsider(string id, string displayName, bool isActive = true, string email = "")
    {
        Directory.Add(id, displayName, email, isActive);

        return id;
    }

    public OrganizationRole RoleOf(string id) => Store.FindMembership(Organization.Id, id)!.Role;
}
