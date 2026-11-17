using Mustask.Domain.Identity;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// feat-043's bounded reads (<see cref="INavigationReadModel"/>), through the reachability specification: a deactivated project, a
/// deleted team and another organization's rows never appear. The statements are fixed in number whatever the volume; the rows are
/// assembled in memory from rows already read (backend.md, "Filtering happens before materialization"). Every statement carries the
/// caller's organization.
/// </summary>
internal sealed class NavigationReadModel(MustaskDbContext context) : INavigationReadModel
{
    public async Task<OverviewRows> ReadOverviewAsync(CallerAccess caller, CancellationToken cancellationToken)
    {
        var organizationId = caller.OrganizationId;
        var sight = SightOf(caller);
        var limit = NavigationLimits.OverviewRows;

        // Statements 1-3: the counts.
        var departmentCount = await Departments(organizationId, sight).CountAsync(cancellationToken);
        var projectCount = await Projects(organizationId, sight).CountAsync(cancellationToken);
        var teamCount = await Teams(organizationId, sight).CountAsync(cancellationToken);

        // Statements 4-6: the first rows of each list, by name in Russian collation then id.
        var departments = await Departments(organizationId, sight)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Take(limit)
            .Select(area => new NavigationRef(area.Id, area.Name))
            .ToListAsync(cancellationToken);

        var projects = await Projects(organizationId, sight)
            .AsNoTracking()
            .OrderBy(project => EF.Functions.Collate(project.Name, OrganizationsCollation.Russian))
            .ThenBy(project => project.Id)
            .Take(limit)
            .Select(project => new NavigationProjectRef(project.Id, project.Name, project.Key))
            .ToListAsync(cancellationToken);

        var teams = await TeamsWithMembership(organizationId, caller.UserId, sight)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Statement 7: the caller's own teams, all of them (a person is in a handful), by name.
        var myTeams = await TeamsWithMembership(organizationId, caller.UserId, sight, onlyMine: true)
            .ToListAsync(cancellationToken);

        return new OverviewRows(departmentCount, projectCount, teamCount, departments, projects, teams, myTeams);
    }

    public async Task<SelectorOfferRows> ReadSelectorOffersAsync(CallerAccess caller, CancellationToken cancellationToken)
    {
        var organizationId = caller.OrganizationId;
        var sight = SightOf(caller);

        // Statement 1: the departments.
        var departments = await Departments(organizationId, sight)
            .OrderBy(area => EF.Functions.Collate(area.Name, OrganizationsCollation.Russian))
            .ThenBy(area => area.Id)
            .Select(area => new OfferDepartmentRow(area.Id, area.Name, area.Color!))
            .ToListAsync(cancellationToken);

        // Statement 2: the active projects with the department holding their node (a project at the top level has none).
        var projects = await (
                from project in Projects(organizationId, sight).AsNoTracking()
                join node in context.Areas.AsNoTracking().Where(area => area.OrganizationId == organizationId && area.Type == AreaType.Project)
                    on (Guid?)project.Id equals node.ProjectId
                join department in Departments(organizationId, sight) on node.ParentId equals (Guid?)department.Id into placeOrNone
                from place in placeOrNone.DefaultIfEmpty()
                orderby EF.Functions.Collate(project.Name, OrganizationsCollation.Russian), project.Id
                select new
                {
                    project.Id,
                    project.Name,
                    project.Key,
                    DepartmentId = place == null ? (Guid?)null : place.Id,
                    DepartmentName = place == null ? null : place.Name,
                    project.Color,
                })
            .ToListAsync(cancellationToken);

        // Statement 3: every link of an active team to an active project.
        var projectLinks = await (
                from link in context.ProjectTeams.AsNoTracking()
                join project in Projects(organizationId, sight) on link.ProjectId equals project.Id
                join team in Teams(organizationId, sight) on link.TeamId equals team.Id
                where link.OrganizationId == organizationId
                select new { link.ProjectId, link.TeamId })
            .ToListAsync(cancellationToken);

        // Statement 4: the active teams with the department each stands in (their place, doc-016 BR-TD-1), the caller's membership read
        // with them.
        var teams = await ActiveTeamPlaces(organizationId, caller.UserId, sight).ToListAsync(cancellationToken);

        var teamsOfProject = projectLinks.ToLookup(link => link.ProjectId, link => link.TeamId);

        return new SelectorOfferRows(
            departments,
            projects
                .Select(project => new OfferProjectRow(
                    project.Id, project.Name, project.Key, project.DepartmentId, project.DepartmentName, teamsOfProject[project.Id].ToList(), project.Color))
                .ToList(),
            teams
                .Select(team => new OfferTeamRow(team.Id, team.Name, team.IsMember, team.DepartmentId, team.Color))
                .ToList());
    }

    private IQueryable<Area> Departments(Guid organizationId, SightedIds? sight)
    {
        var all = context.Areas.AsNoTracking().Where(area => area.OrganizationId == organizationId && area.Type == AreaType.Department);
        var ids = sight?.Departments.ToArray();

        return ids is null ? all : all.Where(area => ids.Contains(area.Id));
    }

    private IQueryable<Project> Projects(Guid organizationId, SightedIds? sight)
    {
        var ids = sight?.Projects.ToArray();

        return ids is null ? context.ReachableProjects(organizationId) : context.ReachableProjects(organizationId).Where(project => ids.Contains(project.Id));
    }

    private IQueryable<Team> Teams(Guid organizationId, SightedIds? sight)
    {
        var ids = sight?.Teams.ToArray();

        return ids is null ? context.ActiveTeamsOf(organizationId) : context.ActiveTeamsOf(organizationId).Where(team => ids.Contains(team.Id));
    }

    private static SightedIds? SightOf(CallerAccess caller) =>
        caller.Sight ?? (caller.Role == OrganizationRole.Admin
            ? null
            : throw new InvalidOperationException("A non-administrator read of the structure needs the caller's sight (BR-TD-17)."));

    /// <summary>The organization's active teams by name, then id, each with whether <paramref name="userId"/> is in it (one EXISTS in the statement).</summary>
    private IQueryable<NavigationTeamRef> TeamsWithMembership(Guid organizationId, string userId, SightedIds? sight, bool onlyMine = false) =>
        Teams(organizationId, sight)
            .AsNoTracking()
            .Where(team => !onlyMine || context.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId))
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new NavigationTeamRef(
                team.Id,
                team.Name,
                context.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId)));

    /// <summary>The organization's active teams by name, then id, with their place and colour and whether <paramref name="userId"/> is in each.</summary>
    private IQueryable<TeamPlaceRow> ActiveTeamPlaces(Guid organizationId, string userId, SightedIds? sight) =>
        Teams(organizationId, sight)
            .AsNoTracking()
            .OrderBy(team => EF.Functions.Collate(team.Name, OrganizationsCollation.Russian))
            .ThenBy(team => team.Id)
            .Select(team => new TeamPlaceRow(
                team.Id,
                team.Name,
                team.DepartmentId!.Value,
                team.Color,
                context.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.UserId == userId)));

    private sealed record TeamPlaceRow(Guid Id, string Name, Guid DepartmentId, string Color, bool IsMember);
}
