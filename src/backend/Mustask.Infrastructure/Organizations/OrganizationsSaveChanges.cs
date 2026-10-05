using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The one flush the four repositories share (plan-backend § 6.3). A
/// handler's uniqueness pre-check and its insert are two round-trips; a
/// concurrent write in between hits the unique index at save time (SQLSTATE
/// 23505, ADR-0011 echelon 1). The business indexes map to the same 409 code
/// as their pre-check, so a client sees one outcome either way. Every other
/// failure, the other unique indexes and every check included, is rethrown:
/// no handler can reach them, so a violation is a defect and stays a 500.
/// </summary>
internal static class OrganizationsSaveChanges
{
    public static async Task SaveAsync(MustaskDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.OrganizationName))
        {
            // The transaction is aborted and cannot read the winner, so the race names the submitted, trimmed name.
            var name = Pending<Organization>(context)?.Name ?? string.Empty;

            throw new ConflictException(
                OrganizationErrorCodes.OrganizationNameTaken,
                new ErrorParams { ["name"] = ErrorParam.Text(name) },
                exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.ProjectName))
        {
            throw new ConflictException(OrganizationErrorCodes.ProjectNameTaken, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.ProjectKey))
        {
            var key = Pending<Project>(context)?.Key ?? string.Empty;

            throw new ConflictException(
                OrganizationErrorCodes.ProjectKeyTaken,
                new ErrorParams { ["key"] = ErrorParam.Text(key) },
                exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.TeamName))
        {
            throw new ConflictException(OrganizationErrorCodes.TeamNameTaken, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.AreaName))
        {
            throw new ConflictException(OrganizationErrorCodes.AreaNameTaken, ErrorParams.None, exception);
        }
    }

    /// <summary>The added or modified entity of that type the failed flush was writing.</summary>
    private static T? Pending<T>(MustaskDbContext context)
        where T : class =>
        context.ChangeTracker.Entries<T>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .FirstOrDefault();
}
