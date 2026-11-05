using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// The arrangement of feat-040's repository-level team view tests: one organization, a team linked to three projects — «Пример
/// проекта» (PRM), «Второй» (SEC) and «Спящий» (DEA, deactivated after its item was written) — and the items that are members of
/// the team's «Истории» backlog and the ones that are not, each non-member for its own reason (the other team's, the Removed
/// state's, another level's, the deactivated project's).
/// </summary>
internal sealed record TeamViewWorld(
    Guid OrganizationId,
    Guid Team,
    Guid OtherTeam,
    Guid FirstProject,
    Guid SecondProject,
    Guid DeactivatedProject,
    TeamBacklogScope Scope,
    ProcessDefinitionRow Process,
    Guid StoryInFirst,
    Guid BugInSecond,
    Guid RemovedInFirst,
    Guid TaskInFirst,
    Guid StoryInDeactivated,
    Guid StoryOfTheOtherTeam)
{
    /// <summary>The two members of the backlog: a story of PRM and a bug of SEC.</summary>
    public IReadOnlyList<Guid> Members => [StoryInFirst, BugInSecond];

    public static async Task<TeamViewWorld> SeedAsync(OrganizationsDatabaseFixture database)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = structure.FirstTeam.Id;
        var other = structure.SecondTeam.Id;
        var first = structure.Project.Project.Id;
        var second = await database.AddProjectAsync(organization, "Второй", "SEC");
        var sleeping = await database.AddProjectAsync(organization, "Спящий", "DEA");
        await database.LinkAsync(organization, second.Project.Id, team);
        await database.LinkAsync(organization, sleeping.Project.Id, team);

        await using var context = database.NewContext();
        var process = await new ProcessDefinitionReadModel(context).FindByOrganizationAsync(organization, CancellationToken.None)
            ?? throw new InvalidOperationException("The organization has a process.");
        var story = process.WorkItemTypes.Single(type => type.Key == "US");
        var bug = process.WorkItemTypes.Single(type => type.Key == "BUG");
        var task = process.WorkItemTypes.Single(type => type.Key == "TASK");
        Assert.Equal(story.BacklogLevelId, bug.BacklogLevelId);
        var firstState = (WorkItemTypeRow type) => type.States.Single(state => state.Position == 1).Id;
        var removed = story.States.Single(state => state.Category == StateCategory.Removed).Id;

        var ids = Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7()).ToArray();
        var rows = new (Guid Id, Guid Project, Guid Root, int Number, WorkItemTypeRow Type, Guid State, Guid Owner)[]
        {
            (ids[0], first, structure.Project.Root.Id, 1, story, firstState(story), team),
            (ids[1], second.Project.Id, second.Root.Id, 1, bug, firstState(bug), team),
            (ids[2], first, structure.Project.Root.Id, 2, story, removed, team),
            (ids[3], first, structure.Project.Root.Id, 3, task, firstState(task), team),
            (ids[4], sleeping.Project.Id, sleeping.Root.Id, 1, story, firstState(story), team),
            (ids[5], first, structure.Project.Root.Id, 4, story, firstState(story), other),
        };

        foreach (var (id, project, root, number, type, state, owner) in rows)
        {
            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description, creation_request_id, created_at, created_by, changed_at)
                VALUES (@id, @project, @number, @type, @state, @area, @team, 'Элемент', '', gen_random_uuid(), now() + @n * interval '1 second', 'team-view-test', now())
                """,
                ("id", id),
                ("project", project),
                ("number", number),
                ("type", type.Id),
                ("state", state),
                ("area", root),
                ("team", owner),
                ("n", (double)number));
        }

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @project", ("project", sleeping.Project.Id));

        var scope = TeamBacklogScope.Of(team, story.BacklogLevelId, process.WorkItemTypes.Where(type => type.BacklogLevelId == story.BacklogLevelId));

        return new TeamViewWorld(organization, team, other, first, second.Project.Id, sleeping.Project.Id, scope, process, ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
    }
}
