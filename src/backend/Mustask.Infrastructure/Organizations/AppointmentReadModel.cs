using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// <see cref="IAppointmentReadModel"/> (feat-033 plan-backend § 4.3). The list is two statements — the count, then the page, with
/// the filter, the order and the page cut in SQL; the newest record of each row is a correlated <c>max</c> served by the record's
/// index. A record is read in one statement and its people named through <see cref="IUserDirectory"/> in <c>ceil(ids / 500)</c>.
/// </summary>
internal sealed class AppointmentReadModel(MustaskDbContext context, IUserDirectory directory) : IAppointmentReadModel
{
    public async Task<RescueOrganizationsPage> GetOrganizationsPageAsync(
        string? nameFilter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var organizations = context.Organizations.AsNoTracking();

        if (!string.IsNullOrEmpty(nameFilter))
        {
            organizations = organizations.Where(organization =>
                EF.Functions.Collate(organization.Name, OrganizationsCollation.Russian).ToLower().Replace("ё", "е")
                    .Contains(EF.Functions.Collate(nameFilter, OrganizationsCollation.Russian).ToLower().Replace("ё", "е")));
        }

        var count = await organizations.CountAsync(cancellationToken);

        var rows = await organizations
            .OrderBy(organization => EF.Functions.Collate(organization.Name, OrganizationsCollation.Russian))
            .ThenBy(organization => organization.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                organization.Status,
                LastAppointedAt = context.OrganizationAppointments
                    .Where(appointment => appointment.OrganizationId == organization.Id)
                    .Max(appointment => (DateTimeOffset?)appointment.AppointedAt),
            })
            .ToListAsync(cancellationToken);

        return new RescueOrganizationsPage(
            count,
            rows.Select(row => new RescueOrganizationRowView(
                    row.Id, row.Name, row.Status == OrganizationStatus.Active, row.LastAppointedAt))
                .ToList());
    }

    public async Task<IReadOnlyList<AppointmentView>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await context.OrganizationAppointments
            .AsNoTracking()
            .Where(appointment => appointment.OrganizationId == organizationId)
            .OrderByDescending(appointment => appointment.AppointedAt)
            .ThenByDescending(appointment => appointment.Id)
            .Select(appointment => new StoredAppointment(
                appointment.Id, appointment.AppointedAt, appointment.AppointedBy, appointment.AppointeeId, appointment.Reason))
            .ToListAsync(cancellationToken);

        return await ViewsAsync(rows, cancellationToken);
    }

    public async Task<AppointmentView?> FindAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var rows = await context.OrganizationAppointments
            .AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId)
            .Select(appointment => new StoredAppointment(
                appointment.Id, appointment.AppointedAt, appointment.AppointedBy, appointment.AppointeeId, appointment.Reason))
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : (await ViewsAsync(rows, cancellationToken))[0];
    }

    public Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.Organizations.AsNoTracking().AnyAsync(organization => organization.Id == organizationId, cancellationToken);

    private async Task<IReadOnlyList<AppointmentView>> ViewsAsync(List<StoredAppointment> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.SelectMany(row => new[] { row.AppointedBy, row.AppointeeId });
        var known = (await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken)).Known;

        return rows
            .Select(row => new AppointmentView(
                row.Id, row.AppointedAt, UserRefView.Of(row.AppointedBy, known), UserRefView.Of(row.AppointeeId, known), row.Reason))
            .ToList();
    }

    private sealed record StoredAppointment(Guid Id, DateTimeOffset AppointedAt, string AppointedBy, string AppointeeId, string Reason);
}
