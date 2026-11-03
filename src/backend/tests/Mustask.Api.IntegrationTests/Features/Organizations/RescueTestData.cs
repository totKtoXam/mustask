using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-033's test data and addresses: the rescue routes, the stored appointment records, and a snapshot of an organization's
/// memberships (so a test can say "nothing else changed" in one comparison). Built without any endpoint of the feature.
/// </summary>
public static class RescueTestData
{
    public const string Rescue = "/api/organizations/rescue";

    public static string AppointmentsPath(Guid organizationId) => $"{Rescue}/{organizationId}/appointments";

    public static string OrganizationAppointmentsPath(Guid organizationId) => $"{OrganizationsApi.Organization(organizationId)}/appointments";

    /// <summary>One membership as stored, for before/after comparisons.</summary>
    public sealed record MembershipRow(
        string UserId, OrganizationRole Role, string AddedBy, DateTimeOffset AddedAt, string? RoleChangedBy, DateTimeOffset? RoleChangedAt);

    public static async Task<List<MembershipRow>> MembershipsAsync(MustaskApiFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .OrderBy(membership => membership.UserId)
            .Select(membership => new MembershipRow(
                membership.UserId, membership.Role, membership.AddedBy, membership.AddedAt, membership.RoleChangedBy, membership.RoleChangedAt))
            .ToListAsync();
    }

    public static async Task<List<OrganizationAppointment>> RecordsAsync(MustaskApiFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().OrganizationAppointments
            .AsNoTracking()
            .Where(appointment => appointment.OrganizationId == organizationId)
            .OrderBy(appointment => appointment.AppointedAt)
            .ToListAsync();
    }

    /// <summary>An organization whose only member is <paramref name="creator"/>-less: nobody belongs to it (the orphaned case).</summary>
    public static Task<Organization> OrphanedOrganizationAsync(
        MustaskApiFactory factory, string? name = null, OrganizationStatus status = OrganizationStatus.Active) =>
        OrganizationsTestData.CreateOrganizationAsync(factory, name, status, founded: false);

    /// <summary>A user the projection knows (a sign-in wrote the row): the id.</summary>
    public static async Task<string> SignedInUserAsync(MustaskApiFactory factory, string firstName, string lastName, string? email = null)
    {
        var id = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, id, firstName, lastName, email);

        return id;
    }

    /// <summary>A system administrator token with a fresh subject, and the subject.</summary>
    public static (string Id, string Token) NewRescuer()
    {
        var id = OrganizationsTokens.NewSubject();

        return (id, OrganizationsTokens.SystemAdmin(id));
    }
}
