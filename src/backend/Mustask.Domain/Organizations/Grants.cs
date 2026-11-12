using Mustask.Domain.Identity;

namespace Mustask.Domain.Organizations;

/// <summary>
/// What the three grant entities share (mod-019 BR-SA-1): a role of one scope held by one active organization membership on
/// one object, who gave or last changed it, and when. At most one per membership and object — the key is
/// <c>(object, user)</c>. The membership is the owner: removing it ends the grant in the same statement (BR-SA-3).
/// </summary>
public interface IGrant
{
    /// <summary>The object's organization, copied from the address chain once; part of the key onto the membership.</summary>
    Guid OrganizationId { get; }

    /// <summary>The department, project or team the grant is held on.</summary>
    Guid ObjectId { get; }

    /// <summary>The grantee's Keycloak subject.</summary>
    string UserId { get; }

    GrantRole Role { get; }

    /// <summary>The user who gave or last changed the grant (BR-SA-1); never the grantee (BR-SA-19).</summary>
    string GrantedBy { get; }

    DateTimeOffset GrantedAt { get; }

    /// <summary>The scope the grant is held on.</summary>
    ResourceScope Scope { get; }
}

/// <summary>The rules every grant obeys, in one place.</summary>
public static class GrantRules
{
    /// <summary>
    /// A grantee is never the grant's giver, on any object, the administrator's included (BR-SA-19). The actor may remove their own
    /// grant, which is not a give or a change and never asks.
    /// </summary>
    public static bool IsSelfGrant(string grantee, string actor) => string.Equals(grantee, actor, StringComparison.Ordinal);

    internal static void EnsureNew(Guid organizationId, Guid objectId, string userId, string grantedBy)
    {
        if (organizationId == Guid.Empty || objectId == Guid.Empty)
        {
            throw new ArgumentException("An organization's and an object's id are never all zeros.");
        }

        if (!OrganizationMembership.IsValidUserId(userId) || !OrganizationMembership.IsValidUserId(grantedBy))
        {
            throw new ArgumentException($"A user is a Keycloak subject: 1 to {OrganizationLimits.UserIdMaxLength} characters, not blank.");
        }

        if (IsSelfGrant(userId, grantedBy))
        {
            throw new ArgumentException("A grantee is never the grant's giver (BR-SA-19).", nameof(grantedBy));
        }
    }
}

/// <summary>A grant held on a department (<c>area_grants</c>). A project node and a sub-area hold none (BR-SA-4, OQ-1 A).</summary>
public sealed class AreaGrant : IGrant
{
    private AreaGrant()
    {
        // EF materialization.
    }

    public Guid OrganizationId { get; private set; }

    public Guid ObjectId { get; private set; }

    public string UserId { get; private set; } = null!;

    public GrantRole Role { get; private set; }

    public string GrantedBy { get; private set; } = null!;

    public DateTimeOffset GrantedAt { get; private set; }

    public ResourceScope Scope => ResourceScope.Area;

    public static AreaGrant Give(Guid organizationId, Guid areaId, string userId, GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(organizationId, areaId, userId, grantedBy);

        return new AreaGrant { OrganizationId = organizationId, ObjectId = areaId, UserId = userId, Role = role, GrantedBy = grantedBy, GrantedAt = now };
    }

    /// <summary>Replaces the role; the giver and the time are those of the change (BR-SA-1).</summary>
    public void Replace(GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(OrganizationId, ObjectId, UserId, grantedBy);
        Role = role;
        GrantedBy = grantedBy;
        GrantedAt = now;
    }
}

/// <summary>A grant held on a project (<c>project_grants</c>); a deactivated project keeps it (BR-SA-3).</summary>
public sealed class ProjectGrant : IGrant
{
    private ProjectGrant()
    {
        // EF materialization.
    }

    public Guid OrganizationId { get; private set; }

    public Guid ObjectId { get; private set; }

    public string UserId { get; private set; } = null!;

    public GrantRole Role { get; private set; }

    public string GrantedBy { get; private set; } = null!;

    public DateTimeOffset GrantedAt { get; private set; }

    public ResourceScope Scope => ResourceScope.Project;

    public static ProjectGrant Give(Guid organizationId, Guid projectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(organizationId, projectId, userId, grantedBy);

        return new ProjectGrant { OrganizationId = organizationId, ObjectId = projectId, UserId = userId, Role = role, GrantedBy = grantedBy, GrantedAt = now };
    }

    /// <summary>Replaces the role; the giver and the time are those of the change (BR-SA-1).</summary>
    public void Replace(GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(OrganizationId, ObjectId, UserId, grantedBy);
        Role = role;
        GrantedBy = grantedBy;
        GrantedAt = now;
    }
}

/// <summary>A grant held on a team (<c>team_grants</c>); team membership grants nothing and a grant needs none (BR-SA-1).</summary>
public sealed class TeamGrant : IGrant
{
    private TeamGrant()
    {
        // EF materialization.
    }

    public Guid OrganizationId { get; private set; }

    public Guid ObjectId { get; private set; }

    public string UserId { get; private set; } = null!;

    public GrantRole Role { get; private set; }

    public string GrantedBy { get; private set; } = null!;

    public DateTimeOffset GrantedAt { get; private set; }

    public ResourceScope Scope => ResourceScope.Team;

    public static TeamGrant Give(Guid organizationId, Guid teamId, string userId, GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(organizationId, teamId, userId, grantedBy);

        return new TeamGrant { OrganizationId = organizationId, ObjectId = teamId, UserId = userId, Role = role, GrantedBy = grantedBy, GrantedAt = now };
    }

    /// <summary>Replaces the role; the giver and the time are those of the change (BR-SA-1).</summary>
    public void Replace(GrantRole role, string grantedBy, DateTimeOffset now)
    {
        GrantRules.EnsureNew(OrganizationId, ObjectId, UserId, grantedBy);
        Role = role;
        GrantedBy = grantedBy;
        GrantedAt = now;
    }
}
