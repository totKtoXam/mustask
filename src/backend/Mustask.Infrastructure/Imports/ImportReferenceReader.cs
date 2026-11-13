using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.WorkItems;

namespace Mustask.Infrastructure.Imports;

/// <summary>
/// <see cref="IImportReferenceReader"/>: a fixed number of read-only statements (projects, types and states, memberships, teams, links, the
/// items the identifiers name) plus one directory lookup for the members' names. Nothing is written, no lock is taken.
/// </summary>
public sealed class ImportReferenceReader(MustaskDbContext context, IUserDirectory directory) : IImportReferenceReader
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<ImportReferences> LoadAsync(Guid organizationId, IReadOnlyCollection<string> identifiers, CancellationToken cancellationToken)
    {
        var projects = await context.Projects.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId)
            .Select(p => new { p.Id, p.Name, p.Key, p.Status })
            .ToListAsync(cancellationToken);

        var processId = await context.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId).Select(o => o.ProcessId).SingleAsync(cancellationToken);

        var types = await context.WorkItemTypes.AsNoTracking().Where(t => t.ProcessId == processId)
            .Select(t => new { t.Id, t.Key, t.Name, t.Appearance }).ToListAsync(cancellationToken);
        var typeIds = types.Select(t => t.Id).ToList();
        var states = await context.StateDefinitions.AsNoTracking().Where(s => typeIds.Contains(s.WorkItemTypeId))
            .OrderBy(s => s.Position).Select(s => new { s.Id, s.WorkItemTypeId, s.Name, s.Category }).ToListAsync(cancellationToken);

        var typeRefs = types.ToDictionary(
            t => t.Id,
            t => new DryRunTypeRef(
                t.Id,
                t.Name,
                t.Appearance,
                states.Where(s => s.WorkItemTypeId == t.Id).Select(s => new DryRunStateRef(s.Id, s.Name, s.Category == StateCategory.Removed)).ToList()));

        var memberIds = await context.OrganizationMemberships.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId).Select(m => m.UserId).ToListAsync(cancellationToken);
        var lookup = memberIds.Count == 0 ? null : await directory.LookupAsync(memberIds, cancellationToken);
        var members = memberIds.ToDictionary(
            id => id,
            id => lookup is not null && lookup.Known.TryGetValue(id, out var user)
                ? new DryRunMemberRef(id, user.DisplayName, user.IsActive)
                : new DryRunMemberRef(id, id, false),
            StringComparer.Ordinal);

        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.Status == TeamStatus.Active)
            .Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken);
        var links = await context.ProjectTeams.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId).Select(l => new { l.ProjectId, l.TeamId }).ToListAsync(cancellationToken);
        var activeTeams = teams.Select(t => t.Id).ToHashSet();

        var projectTeams = links.Where(l => activeTeams.Contains(l.TeamId)).GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(l => l.TeamId).ToHashSet());

        var items = await LoadItemsAsync(organizationId, identifiers, projects.Select(p => (p.Id, p.Key)).ToList(), typeRefs, types.ToDictionary(t => t.Id, t => t.Key), cancellationToken);

        return new ImportReferences(
            projects.ToDictionary(p => p.Id, p => new DryRunProjectRef(p.Id, p.Name, p.Status == ProjectStatus.Active)),
            typeRefs,
            members,
            teams.ToDictionary(t => t.Id, t => t.Name),
            projectTeams,
            items);
    }

    private async Task<IReadOnlyDictionary<string, DryRunItemRef>> LoadItemsAsync(
        Guid organizationId,
        IReadOnlyCollection<string> identifiers,
        List<(Guid Id, string Key)> projects,
        IReadOnlyDictionary<Guid, DryRunTypeRef> typeRefs,
        IReadOnlyDictionary<Guid, string> typeKeys,
        CancellationToken cancellationToken)
    {
        var wanted = new List<(Guid ProjectId, string ProjectKey, string TypeKey, int Number)>();

        foreach (var text in identifiers)
        {
            if (WorkItemIdentifier.TryParse(DryRunEngine.NormalizeIdentifier(text), out var identifier))
            {
                var project = projects.FirstOrDefault(p => p.Key == identifier.ProjectKey);

                if (project.Key is not null)
                {
                    wanted.Add((project.Id, identifier.ProjectKey, identifier.TypeKey, identifier.Number));
                }
            }
        }

        var result = new Dictionary<string, DryRunItemRef>(StringComparer.Ordinal);

        if (wanted.Count == 0)
        {
            return result;
        }

        var projectIds = wanted.Select(w => w.ProjectId).Distinct().ToList();
        var numbers = wanted.Select(w => w.Number).Distinct().ToList();
        var removed = WorkItemSets.RemovedStateIds(context);

        var found = await WorkItemSets.InAnyState(context).AsNoTracking()
            .Where(i => projectIds.Contains(i.ProjectId) && numbers.Contains(i.Number))
            .Select(i => new { Item = i, Deleted = removed.Contains(i.StateId) })
            .ToListAsync(cancellationToken);

        foreach (var row in found)
        {
            var item = row.Item;
            var key = wanted.FirstOrDefault(w => w.ProjectId == item.ProjectId && w.Number == item.Number
                && typeKeys.TryGetValue(item.TypeId, out var typeKey) && typeKey == w.TypeKey);

            if (key.ProjectKey is null)
            {
                continue;
            }

            var identifier = WorkItemIdentifier.Format(key.ProjectKey, key.TypeKey, item.Number);
            result[DryRunEngine.NormalizeIdentifier(identifier)] = new DryRunItemRef(item.Id, identifier, item.ProjectId, item.TypeId, row.Deleted, ValuesOf(item));
        }

        return result;
    }

    private static Dictionary<string, string?> ValuesOf(WorkItem item) => new(StringComparer.Ordinal)
    {
        [ImportFieldCatalogue.Title] = item.Title,
        [ImportFieldCatalogue.Description] = item.Description,
        [ImportFieldCatalogue.State] = item.StateId.ToString(),
        [ImportFieldCatalogue.Assignee] = item.AssigneeId,
        [ImportFieldCatalogue.Priority] = item.Priority.ToString(Invariant),
        [ImportFieldCatalogue.OwningTeam] = item.OwningTeamId?.ToString(),
        [ImportFieldCatalogue.Parent] = item.ParentId?.ToString(),
        [ImportFieldCatalogue.Risk] = Number(item.Risk),
        [ImportFieldCatalogue.StoryPoints] = Number(item.StoryPoints),
        [ImportFieldCatalogue.Effort] = Number(item.Effort),
        [ImportFieldCatalogue.BusinessValue] = Number(item.BusinessValue),
        [ImportFieldCatalogue.TimeCriticality] = Number(item.TimeCriticality),
        [ImportFieldCatalogue.StartDate] = item.StartDate?.ToString("yyyy-MM-dd", Invariant),
        [ImportFieldCatalogue.TargetDate] = item.TargetDate?.ToString("yyyy-MM-dd", Invariant),
        [ImportFieldCatalogue.Severity] = Number(item.Severity),
        [ImportFieldCatalogue.Activity] = item.Activity?.ToString(),
        [ImportFieldCatalogue.OriginalEstimate] = Number(item.OriginalEstimate),
        [ImportFieldCatalogue.RemainingWork] = Number(item.RemainingWork),
        [ImportFieldCatalogue.CompletedWork] = Number(item.CompletedWork),
        [ImportFieldCatalogue.ValueArea] = item.ValueArea?.ToString(),
        [ImportFieldCatalogue.ReproSteps] = item.ReproSteps,
        [ImportFieldCatalogue.SystemInfo] = item.SystemInfo,
    };

    private static string? Number(decimal? value) => value?.ToString("0.##", Invariant);

    private static string? Number(int? value) => value?.ToString(Invariant);
}
