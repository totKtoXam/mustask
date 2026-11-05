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
/// as their pre-check, so a client sees one outcome either way. The foreign
/// keys of the team membership (mod-016) and of the project's default team
/// (mod-017) are the backstops of locks and answer as the absence they mean. Every other
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
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.MembershipPrimaryKey))
        {
            // mod-016 BR-M1-5: the same user added to an organization twice; a lost race answers like the pre-check.
            throw new ConflictException(OrganizationErrorCodes.MembershipAlreadyMember, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(exception, OrganizationsConstraints.TeamMembershipPrimaryKey))
        {
            // mod-016: the same user added to a team twice.
            throw new ConflictException(OrganizationErrorCodes.TeamMemberExists, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, OrganizationsConstraints.TeamMembershipOrganizationMembership))
        {
            // mod-016 BR-M1-10: the organization membership ended while a team membership was being added.
            throw new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, OrganizationsConstraints.ProjectDefaultTeamKey))
        {
            // mod-017 ADR-0028 § 4, plan-backend D-15 (feat-028 D-10): the default key refused a team that is not linked. The link
            // lock of SetDefaultTeam makes this unreachable; if a race ever reaches it, it answers as the team's absence does,
            // never as a 500.
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound, ErrorParams.None, exception);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, OrganizationsConstraints.TeamMembershipTeamKey))
        {
            // feat-028 plan-backend § 8 "AddTeam": a team added to a team that is not there (a membership of a team the same flush
            // did not insert, or of another organization). The team lock makes this unreachable; it answers as the team's absence.
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound, ErrorParams.None, exception);
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
