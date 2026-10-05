using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Locks by id with raw SQL, then loads with the regular tracking query
/// (ADR-0010, "Consequences"). Under READ COMMITTED the load is a new
/// statement, so it sees the row as the lock holder left it.
/// </summary>
internal sealed class OrganizationRepository(MustaskDbContext context) : IOrganizationRepository
{
    public Task<string?> FindNameHolderAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Organizations
            .AsNoTracking()
            .Where(organization => organization.NormalizedName == normalizedName && organization.Id != exceptId)
            .Select(organization => organization.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Organization?> LockAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM organizations WHERE id = {organizationId} FOR NO KEY UPDATE",
            cancellationToken);

        return await context.Organizations.SingleOrDefaultAsync(organization => organization.Id == organizationId, cancellationToken);
    }

    public void Add(Organization organization) => context.Organizations.Add(organization);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
