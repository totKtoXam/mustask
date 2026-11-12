using System.Globalization;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations.GetSprintParticipants;

/// <summary>
/// The results of a Closed sprint by participant (US-10, BR-15, BR-16): each person's figures in the sprint at the level, and over
/// the range sprints in which they held items, min, average and max with «k из N». Rows by current full name, then
/// «Без исполнителя» last, 200 a page. One grouped read of the snapshot covers the sprint and the range; the names and the team's
/// membership are one read each, so the statements are fixed whatever the number of people.
/// </summary>
public sealed class GetSprintParticipantsHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    ISprintReadModel sprints,
    IUserDirectory directory)
{
    private const int LookupBatch = 500;

    public async Task<GetSprintParticipantsResponse> Handle(GetSprintParticipantsQuery query, CancellationToken cancellationToken)
    {
        var scope = await OutcomeScope.ResolveAsync(
            structure, processes, sprints, query.OrganizationId, query.TeamId, query.SprintId, query.Level, query.From, query.To,
            cancellationToken);

        var rangeIds = scope.RangeSprints.Sprints.Select(sprint => sprint.Id).ToList();
        var counts = await sprints.CountsByParticipantAsync(rangeIds.Append(scope.Sprint.Id).ToList(), scope.LevelId, cancellationToken);

        // Who is a row: whoever held items at the level in this sprint. Their figures in the range are of the other sprints.
        var current = counts.Where(row => row.SprintId == scope.Sprint.Id).ToDictionary(row => row.AssigneeId ?? string.Empty);
        var history = counts.Where(row => row.SprintId != scope.Sprint.Id).ToLookup(row => row.AssigneeId ?? string.Empty);

        var userIds = current.Keys.Where(key => key.Length > 0).ToList();
        var names = await NamesAsync(userIds, cancellationToken);
        var members = userIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : await sprints.TeamMemberIdsAsync(scope.TeamId, userIds, cancellationToken);

        ParticipantResponse Build(string? userId, string? displayName, bool isMember, SprintCounts own, IReadOnlyCollection<SprintCounts> series)
        {
            var row = SprintFiguresRow.Of(own);

            return new ParticipantResponse(
                userId, displayName, isMember, row.Items, row.StoryPoints, row.ClosedStoryPoints, row.ClosedStoryPointsPercent,
                row.ClosedItems, row.ClosedItemsPercent, series.Count, DynamicsFigures.Of(series));
        }

        IReadOnlyCollection<SprintCounts> SeriesOf(string key) => history[key].Select(row => row.Counts).ToList();

        // Known people by their current name, those the directory does not know after them by id, «Без исполнителя» last.
        var collation = CultureInfo.GetCultureInfo("ru-RU").CompareInfo;
        var people = userIds
            .OrderBy(id => names.ContainsKey(id) ? 0 : 1)
            .ThenBy(id => names.GetValueOrDefault(id) ?? string.Empty, Comparer<string>.Create((a, b) => collation.Compare(a, b, CompareOptions.IgnoreCase)))
            .ThenBy(id => id, StringComparer.Ordinal)
            .Select(id => Build(id, names.GetValueOrDefault(id), members.Contains(id), current[id].Counts, SeriesOf(id)))
            .ToList();

        if (current.TryGetValue(string.Empty, out var unassigned))
        {
            people.Add(Build(null, null, false, unassigned.Counts, SeriesOf(string.Empty)));
        }

        // «Итого»: the sprint's own figures at the level, and the dynamics of the range sprints as a whole.
        var ownTotal = new SprintCounts(
            current.Values.Sum(row => row.Counts.Items),
            current.Values.Sum(row => row.Counts.StoryPoints),
            current.Values.Sum(row => row.Counts.ClosedItems),
            current.Values.Sum(row => row.Counts.ClosedStoryPoints));

        var rangeTotals = rangeIds
            .Select(id => counts.Where(row => row.SprintId == id).Select(row => row.Counts).ToList())
            .Where(rows => rows.Count > 0)
            .Select(rows => new SprintCounts(
                rows.Sum(c => c.Items), rows.Sum(c => c.StoryPoints), rows.Sum(c => c.ClosedItems), rows.Sum(c => c.ClosedStoryPoints)))
            .ToList();

        var offset = int.TryParse(query.Offset, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        return new GetSprintParticipantsResponse(
            scope.LevelName,
            scope.Range.From,
            scope.Range.To,
            scope.RangeSprints.Total,
            people.Skip(offset).Take(SprintLimits.PageSize).ToList(),
            Build(null, null, false, ownTotal, rangeTotals),
            people.Count);
    }

    /// <summary>The directory's current names, in batches of the directory's limit; an unknown id has none.</summary>
    private async Task<Dictionary<string, string>> NamesAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(LookupBatch))
        {
            foreach (var (id, user) in (await directory.LookupAsync(batch, cancellationToken)).Known)
            {
                names[id] = user.DisplayName;
            }
        }

        return names;
    }
}
