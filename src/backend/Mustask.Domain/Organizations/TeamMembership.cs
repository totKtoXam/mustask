namespace Mustask.Domain.Organizations;

/// <summary>
/// A user's membership in one team (spec § Entities). It has no role and no
/// who/when, and grants no code (doc-006 § Roles). It is keyed by the team's id,
/// so it survives feat-028's move of teams to the organization (status.md D-2).
/// Team members are a subset of the organization's members (BR-M1-10): the
/// organization is copied from the team's address chain once, so the database can
/// hold the rule as a composite foreign key to the organization membership. A team
/// never changes organization, so the copy cannot go stale.
/// </summary>
public sealed class TeamMembership
{
    private TeamMembership()
    {
        // EF materialization.
    }

    private TeamMembership(Guid teamId, Guid organizationId, string userId)
    {
        TeamId = teamId;
        OrganizationId = organizationId;
        UserId = userId;
    }

    public Guid TeamId { get; private set; }

    /// <summary>The team's organization, as the address chain resolved it. Part of the key of the organization membership.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>The member's Keycloak subject.</summary>
    public string UserId { get; private set; } = null!;

    public static TeamMembership Add(Guid teamId, Guid organizationId, string userId)
    {
        if (teamId == Guid.Empty || organizationId == Guid.Empty)
        {
            throw new ArgumentException("A team's and an organization's id are never all zeros.");
        }

        if (!OrganizationMembership.IsValidUserId(userId))
        {
            throw new ArgumentException(
                $"A user is a Keycloak subject: 1 to {OrganizationLimits.UserIdMaxLength} characters, not blank.",
                nameof(userId));
        }

        return new TeamMembership(teamId, organizationId, userId);
    }
}
