using System.Reflection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// mod-005 BR-DR-8 (ADR-0030 rule 3, plan-backend D-P4): every write route of the organization-addressed set is classified in a
/// table beside this test — it <b>refuses a deleted item</b> (with its code and the test that proves it), <b>acts on one</b>
/// (Restore, Purge), is a <b>clean-up</b> of the third kind (a team's deletion releasing the items it owns, in any state), or
/// <b>names no work item</b> (with its reason). A route with no row fails <c>scripts/verify.sh</c> as
/// <c>OrganizationAccessInventoryTests</c> fails one with no code; a row for a route that is gone fails too, and so does a row that
/// names a proving test that does not exist, so a row cannot lie. The superset (every write route, not only those that carry an
/// identifier) is the point: nothing here has to decide which body "names work items" — a new bulk route whose body names item ids
/// cannot pass unseen.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemWriteInventoryTests(MustaskApiFactory factory)
{
    private enum Kind
    {
        RefusesADeletedItem,
        ActsOnOne,
        CleanUp,
        NamesNoWorkItem,
    }

    /// <param name="Kind">How the route treats an item in «Удалена».</param>
    /// <param name="Detail">The code it answers (refuses), what it does (acts, clean-up), or why it names no work item.</param>
    /// <param name="Proof">The test that proves the row, <c>Class.Method</c>; none for a route that names no work item.</param>
    private sealed record Row(Kind Kind, string Detail, string? Proof = null);

    private const string Org = "/api/organizations/{organizationId}";
    private const string Project = Org + "/projects/{projectId}";

    // feat-028: a team is the organization's (OrgTeam, with its name, description, members, links and deletion); the project-nested
    // address (Team) is the team seen from a project — its backlog, its board and the unlink of its link.
    private const string OrgTeam = Org + "/teams/{teamId}";
    private const string Team = Project + "/teams/{teamId}";
    private const string Area = Project + "/areas/{areaId}";
    private const string Item = Project + "/workitems/{identifier}";

    private const string ReasonContainer = "acts on a container (organization, project) and names no work item; its items stay as they are";
    private const string ReasonStructure = "changes a team or an area and names no work item";
    private const string ReasonMembership = "changes who belongs to the organization or a team, and names no work item";

    /// <summary>The table. A line that adds a write route adds its row here, with the test that proves it.</summary>
    private static readonly IReadOnlyDictionary<string, Row> Table = new Dictionary<string, Row>(StringComparer.Ordinal)
    {
        // mod-005: the item's own writes.
        ["PATCH " + Item] = new(Kind.RefusesADeletedItem, "workitems.workitem.deleted", "DeletedItemIsReadOnlyEndpointTests.Patch_ADeletedItem_EmptyBody_Is409Deleted"),
        ["POST " + Item + "/delete"] = new(Kind.RefusesADeletedItem, "workitems.workitem.deleted", "DeleteWorkItemEndpointTests.Delete_AlreadyDeleted_Is409Deleted_TheHeldStateIsTheFirstDeletions"),
        ["POST " + Item + "/restore"] = new(Kind.ActsOnOne, "restores it; refuses a live item with workitems.workitem.not_deleted", "RestoreWorkItemEndpointTests.Restore_ALiveItem_Is409NotDeleted_EvenWithAForeignOrRemovedState_StateNotApplied"),
        ["POST " + Item + "/purge"] = new(Kind.ActsOnOne, "removes it; refuses a live item with workitems.workitem.not_deleted", "PurgeWorkItemEndpointTests.Purge_ALiveItem_Is409NotDeleted_NothingRemoved"),

        // feat-001 and feat-027: the link writes keep their own refusal.
        ["PUT " + Item + "/parent"] = new(Kind.RefusesADeletedItem, "workitems.link.item_deleted", "DeleteDoesNotCascadeEndpointTests.SetParent_ToADeletedParent_AndFromADeletedChild_Is409ItemDeleted_NothingLinked"),
        ["POST " + Project + "/workitems"] = new(Kind.RefusesADeletedItem, "workitems.link.item_deleted (a creation beneath a deleted parent)", "CreateChildEndpointTests.Create_ADeletedParent_Returns409ItemDeleted_NothingCreated"),

        // mod-006 and feat-039: a reorder naming a deleted item is stale.
        ["POST " + OrgTeam + "/backlogs/{level}/reorder"] = new(Kind.RefusesADeletedItem, "boards.backlog.reorder_stale", "DeletedItemIsReadOnlyEndpointTests.Reorder_TeamAndProject_NamingADeletedItemAsMovedOrAnchor_Is409ReorderStale_OrderUnchanged"),
        ["POST " + Project + "/backlogs/{level}/reorder"] = new(Kind.RefusesADeletedItem, "boards.backlog.reorder_stale", "DeletedItemIsReadOnlyEndpointTests.Reorder_TeamAndProject_NamingADeletedItemAsMovedOrAnchor_Is409ReorderStale_OrderUnchanged"),

        // mod-017 and feat-028: the system clean-ups of the third kind — they reach an item in any state and take no EnsureLive. A
        // team's deletion releases what it owns to «Без команды»; an unlink (BR-DR-9, owed to the second of the two lines) reassigns
        // what the team owns in that project to the destination named; neither moves a deleted item out of «Удалена».
        ["DELETE " + OrgTeam] = new(Kind.CleanUp, "releases the items the team owns in any state; a deleted one stays deleted", "DeletedItemViewsEndpointTests.DeleteTeam_ReleasesADeletedItemToo_StillDeleted_ChangedAtMoves"),
        ["POST " + Team + "/unlink"] = new(Kind.CleanUp, "reassigns the items the team owns in the project, in any state, to the destination; a deleted one stays deleted", "DeletedItemViewsEndpointTests.Unlink_ReassignsADeletedItemToo_StillDeleted"),

        // mod-002: containers and structure.
        ["PUT " + Org + "/name"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Org + "/deactivate"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Org + "/restore"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Org + "/projects"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["PUT " + Project + "/name"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Project + "/deactivate"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Project + "/restore"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["PUT " + Project + "/default-team"] = new(Kind.NamesNoWorkItem, ReasonStructure),

        // feat-028: the organization's teams and their links to projects name no work item (a link moves none; only an unlink or a
        // deletion does, above).
        ["PUT " + Project + "/description"] = new(Kind.NamesNoWorkItem, ReasonContainer),
        ["POST " + Project + "/teams"] = new(Kind.NamesNoWorkItem, "links teams to the project and moves no item (feat-028 BR-OL-4)"),
        ["POST " + Org + "/teams"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["PUT " + OrgTeam + "/name"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["PUT " + OrgTeam + "/description"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["POST " + OrgTeam + "/projects"] = new(Kind.NamesNoWorkItem, "links projects to the team and moves no item (feat-028 BR-OL-4)"),
        ["POST " + Project + "/areas"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["PUT " + Area + "/name"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["PUT " + Area + "/parent"] = new(Kind.NamesNoWorkItem, ReasonStructure),
        ["DELETE " + Area] = new(Kind.NamesNoWorkItem, "names no work item; it counts the items in the area, deleted ones too (mod-003 BR-M3-24)"),

        // mod-016: membership.
        ["POST " + Org + "/members"] = new(Kind.NamesNoWorkItem, ReasonMembership),
        ["PUT " + Org + "/members/{userId}/role"] = new(Kind.NamesNoWorkItem, ReasonMembership),
        ["DELETE " + Org + "/members/{userId}"] = new(Kind.NamesNoWorkItem, ReasonMembership),
        ["POST " + OrgTeam + "/members"] = new(Kind.NamesNoWorkItem, ReasonMembership),
        ["DELETE " + OrgTeam + "/members/{userId}"] = new(Kind.NamesNoWorkItem, ReasonMembership),
    };

    private static bool IsWrite(string method) => method is "POST" or "PUT" or "PATCH" or "DELETE";

    /// <summary>Every write route of the live endpoint data source that carries an organization id, as "METHOD /template".</summary>
    private List<string> WriteRoutes()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.GetParameter("organizationId") is not null)
            .SelectMany(endpoint =>
            {
                var template = "/" + endpoint.RoutePattern.RawText!.Trim('/');
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

                return methods.Where(IsWrite).Select(method => $"{method} {template}");
            })
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The problems of a table against the routes that exist and the tests that exist.</summary>
    private static List<string> Problems(IEnumerable<string> routes, IReadOnlyDictionary<string, Row> table, Func<string, bool> testExists)
    {
        var problems = new List<string>();
        var existing = routes.ToHashSet(StringComparer.Ordinal);

        foreach (var route in existing.Where(route => !table.ContainsKey(route)))
        {
            problems.Add($"{route}: a write route with no row — classify it: refuses a deleted item, acts on one, clean-up, or names no work item (ADR-0030 rule 3).");
        }

        foreach (var key in table.Keys.Where(key => !existing.Contains(key)))
        {
            problems.Add($"{key}: a row for a route that does not exist — remove the stale row.");
        }

        foreach (var (key, row) in table)
        {
            if (row.Kind == Kind.NamesNoWorkItem)
            {
                if (string.IsNullOrWhiteSpace(row.Detail))
                {
                    problems.Add($"{key}: a route that names no work item needs its reason.");
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Proof) || !testExists(row.Proof))
            {
                problems.Add($"{key}: its proving test '{row.Proof}' does not exist.");
            }
        }

        return problems;
    }

    private static bool TestExists(string proof)
    {
        var parts = proof.Split('.');

        if (parts.Length != 2)
        {
            return false;
        }

        return typeof(WorkItemWriteInventoryTests).Assembly.GetTypes()
            .Where(type => type.Name == parts[0])
            .Any(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(method => method.Name == parts[1]));
    }

    // Canary: the scan reaches the routes of the work items, the backlogs and the teams, or the tests below would pass on an empty set.
    [Fact]
    public void TheScan_ReachesTheWorkItemBacklogAndTeamRoutes()
    {
        var routes = WriteRoutes();

        Assert.True(routes.Count >= 25, $"Only {routes.Count} write routes were found.");
        Assert.Contains(routes, route => route.Contains("/workitems/", StringComparison.Ordinal));
        Assert.Contains(routes, route => route.Contains("/backlogs/", StringComparison.Ordinal));
        Assert.Contains(routes, route => route.Contains("/teams", StringComparison.Ordinal));
        Assert.Contains(routes, route => route.EndsWith("/purge", StringComparison.Ordinal));
    }

    // ADR-0030 rule 3 · every write route is classified, no row is stale, and no row names a proof that does not exist.
    [Fact]
    public void EveryWriteRoute_IsClassified_NoRowIsStale_EveryProofExists()
    {
        var problems = Problems(WriteRoutes(), Table, TestExists);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // The table is also the route inventory's: the same write routes as OrganizationAddressedRoutes.
    [Fact]
    public void TheTable_IsTheRouteInventorysWriteRoutes()
    {
        var inventory = OrganizationAddressedRoutes.All.Where(route => route.IsWrite).Select(route => $"{route.Method} {route.Template}").Order(StringComparer.Ordinal);

        Assert.Equal(inventory, Table.Keys.Order(StringComparer.Ordinal));
    }

    // The check itself, on planted data: a route with no row, a row for a route that is gone, and a proof that does not exist.
    [Fact]
    public void TheCheck_FailsOnAnUnclassifiedRoute_AStaleRow_AndAProofThatDoesNotExist()
    {
        var routes = WriteRoutes();

        Assert.Contains(
            Problems([.. routes, "POST " + Project + "/workitems/bulk-move"], Table, TestExists),
            problem => problem.StartsWith("POST " + Project + "/workitems/bulk-move: a write route with no row", StringComparison.Ordinal));

        var stale = new Dictionary<string, Row>(Table) { ["POST " + Item + "/archive"] = new(Kind.NamesNoWorkItem, "gone") };
        Assert.Contains(Problems(routes, stale, TestExists), problem => problem.StartsWith("POST " + Item + "/archive: a row for a route", StringComparison.Ordinal));

        var lying = new Dictionary<string, Row>(Table) { ["PATCH " + Item] = new(Kind.RefusesADeletedItem, "workitems.workitem.deleted", "NoSuchTests.No_Such_Test") };
        Assert.Contains(Problems(routes, lying, TestExists), problem => problem.StartsWith("PATCH " + Item + ": its proving test", StringComparison.Ordinal));

        var reasonless = new Dictionary<string, Row>(Table) { ["PUT " + Org + "/name"] = new(Kind.NamesNoWorkItem, " ") };
        Assert.Contains(Problems(routes, reasonless, TestExists), problem => problem.StartsWith("PUT " + Org + "/name: a route that names no work item needs", StringComparison.Ordinal));

        Assert.Empty(Problems(routes, Table, TestExists));
    }
}
