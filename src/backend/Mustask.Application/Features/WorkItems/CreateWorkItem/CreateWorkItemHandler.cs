using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// The create dialog's submission (US-1): a new item of the chosen type with
/// only a title, in its type's initial state, unassigned, with an empty
/// description, in the project's root area (BR-M3-20), owned by the team the
/// dialog shows (mod-017 BR-OT-3) — a team of the project, «Без команды», or, when
/// the request sends none, the project default team as stored when the item is saved;
/// or, created from a backlog row, beneath that row's item, in its area as stored
/// (a classification path, BR-OT-10) and owned by the row's team as stored then,
/// else the project default (feat-027 BR-T-10) — numbered from the project's
/// one sequence (BR-M3-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4, feat-027 api-contract § 3,
/// mod-016 api-contract § 4.2): the scoped check has answered for the organization
/// and the route's code (<c>workitems.workitem.write</c>) before the handler runs;
/// then, when the create carries a <c>parentId</c>, the second code — a parent
/// write needs <c>workitems.link.write</c> too, and its 403 comes before every
/// other answer about the parent or the project; then the address from the
/// outside in (the project must be the organization's and reachable);
/// the values, with every field reason at once; the type; the replay; mod-017's team,
/// when one is sent and no parent is named (404, or 409 for another project's team);
/// the parent, when one is named —
/// its not-found, then the domain's 409s (the deleted parent, the level, the
/// depth bound of feat-027 D-9); then the creation. A creation that names a parent
/// reads no team: it is never refused for one (BR-OT-3, BR-OT-13).
/// </para>
/// <para>
/// <b>The link locks</b> (feat-028 plan-backend § 7, D-3, D-4; mod-017 D-14; ADR-0028 rule 8). Before the number's lock, an explicit
/// team of a creation with no parent has its <b>link to the project</b> locked <c>FOR SHARE</c> as a binder; every other creation
/// that may default — no team sent, or a child, whether or not its parent has a team — locks <b>every link of the project</b>
/// <c>FOR SHARE</c> in one statement ordered by team id and reads the default once, over the links that statement held. An unlink of
/// a team and a deletion of a team hold the link <c>FOR NO KEY UPDATE</c>, so they commit before the creation (it then reads the team
/// not linked — 409 — or deleted — 404 —, its parent reassigned and the default as it stands after them) or wait for it and take the
/// new item with the rest; a child beneath an item of a team being unlinked or deleted is never refused (the parent's team needs no
/// lock of its own: the creation holds the links the unlink needs, and the child's owner key takes only a key share of the link).
/// Every link lock a creation takes is in the unlink's and the deletion's order, so the cycle of mod-017 CR-7 cannot form.
/// <b>"Linked" is what the lock statement returned</b>, never a second read of the links (feat-028 review-security S-1, review-code
/// CR-4): under READ COMMITTED a link committed between the two statements would read linked and be the owner without being held,
/// and an unlink or a deletion of it would then wait on the creation's key share and fail at its final <c>DELETE</c>. A link
/// committed after the lock is therefore not linked as far as this creation goes — an explicit team on it is 409, and it is never the
/// default — as if the creation had run a moment before the link.
/// </para>
/// <para>
/// <b>The number and the replay</b> (BR-M3-5, BR-M3-26). The project's counter
/// row is locked <c>FOR UPDATE</c> first, so the creations of a project run
/// one after another from there to their commit, and each takes one more than
/// the last number issued. The replay check comes after that lock, as a new
/// statement under READ COMMITTED: a submission that arrives twice at the same
/// moment waits for the first to commit, then finds its item and answers it,
/// with no number consumed. The unique index on the submission key is the
/// backstop. The root area needs no lock: it is never deleted (BR-M1-11).
/// </para>
/// <para>
/// <b>The parent</b> (feat-027 api-contract § 3, "Concurrency"): after the
/// number's and the team's locks, the parent write's own order — the project's
/// hierarchy, then the parent as a link end — so the parent is read as the last
/// parent write, save or deletion of it committed. Its area needs no lock: an
/// area that holds an item is never deleted (mod-003 § 6), and the parent's lock
/// keeps it there until the commit. A refusal rolls the transaction back, the
/// number's increment with it.
/// </para>
/// </remarks>
public sealed class CreateWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateWorkItemResult> Handle(CreateWorkItemCommand command, CancellationToken cancellationToken)
    {
        // 0. The second code of a parent write (mod-016 api-contract § 4.2): checked first, before the project or the parent is
        // looked at, so a role without it learns nothing about either. Under the bundles of D-8 the two codes never differ.
        if (command.ParentId is not null && !access.Holds(PermissionCatalog.WorkItemsLinkWrite))
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || await structure.FindProjectAsync(organizationId, projectId, ct) is not { IsReachable: true } project)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. The values, every field reason at once.
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

                if (!AddressedId.TryParse(command.RequestId, out var requestId))
                {
                    errors["requestId"] = [new FieldError(WorkItemErrorCodes.RequestIdInvalid)];
                }

                if (WorkItemText.ValidateTitle(command.Title, out var title) is { } titleReason)
                {
                    errors["title"] = [new FieldError(titleReason)];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(WorkItemErrorCodes.ValidationFailed, errors);
                }

                // 3. The type: one of the organization's process (BR-M3-1).
                if (await processes.FindByOrganizationAsync(organizationId, ct) is not { } process
                    || TypeOf(process, command.TypeId) is not { } type)
                {
                    throw new NotFoundException(WorkItemErrorCodes.TypeNotFound);
                }

                // 4. The link lock, before the number's (feat-028 plan-backend § 7, ADR-0028 rule 8), then the number and the
                // replay after its lock. An explicit team with no parent has its link to the project locked as a binder; every
                // other creation that may take the default — no team sent, or a child, whatever team the parent has — locks
                // every link of the project and reads the default once under that lock, so an unlink or a deletion of a team
                // commits before it or waits for it, never in between (mod-017 D-14). A creation that sends «Без команды» owns
                // nothing: it locks nothing.
                Guid? lockedDefaultTeamId = null;
                var namedLinkHeld = false;

                if (command.ParentId is null && command.Team is { } named)
                {
                    if (named.TeamId is not null && AddressedId.TryParse(named.TeamId, out var namedTeamId))
                    {
                        // The lock statement returns the link it held: that answer, not a second read, says "linked" (S-1).
                        namedLinkHeld = await workItems.LockTeamLinkAsync(organizationId, projectId, namedTeamId, ct);
                    }
                }
                else
                {
                    lockedDefaultTeamId = await workItems.LockProjectLinksAsync(organizationId, projectId, ct);
                }

                await workItems.LockNumberSequenceAsync(projectId, ct);

                if (await workItems.FindCreatedByRequestAsync(projectId, requestId, ct) is { } existingId)
                {
                    return new CreateWorkItemResult(await readModel.GetAsync(existingId, ct), Created: false);
                }

                // 5. The team (feat-028 api-contract § 0.3, BR-OL-6): an explicit team of a creation with no parent is judged
                // now — the team itself on a fresh read under the lock taken before the number's, "linked" by what that lock
                // held — a malformed, unknown, foreign or deleted team answers 404, an active team of the organization that is
                // not linked to the project 409. «Без команды» sent explicitly is none. No team sent takes the project default,
                // resolved over the links' lock above. A creation that names a parent reads no team at all (mod-017 BR-OT-3): it
                // is never refused for one.
                Guid? owningTeamId = lockedDefaultTeamId;

                if (command.ParentId is null && command.Team is { } sentTeam)
                {
                    owningTeamId = await DecideTeamAsync(sentTeam, organizationId, projectId, namedLinkHeld, ct);
                }

                // 6. feat-027: the parent, under the parent write's order — the hierarchy, then the parent itself. One
                // malformed, unknown or of another project answers alike (BR-H-1); its area is the child's area, a
                // classification path (BR-OT-10), and its team, as stored under the parent's lock, is the child's.
                var areaId = project.RootAreaId;
                WorkItem? parent = null;
                IReadOnlyCollection<HierarchyLink> parentAncestry = [];

                if (command.ParentId is not null)
                {
                    if (!AddressedId.TryParse(command.ParentId, out var parentId))
                    {
                        throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);
                    }

                    await workItems.LockHierarchyAsync(projectId, ct);

                    parent = await workItems.LockNewParentAsync(projectId, parentId, ct)
                        ?? throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);
                    parentAncestry = await workItems.AncestryAsync(projectId, parent.Id, ct);
                    owningTeamId = ProjectTeams.ChildTeam(parent.OwningTeamId, lockedDefaultTeamId);
                }

                var number = await workItems.TakeNextNumberAsync(projectId, ct);

                var item = parent is null
                    ? WorkItem.Create(projectId, number, type, areaId, title, requestId, clock.UtcNow, currentUser.SubjectId, owningTeamId)
                    : WorkItemHierarchy.CreateChild(
                        parent, parentAncestry, ProcessHierarchy.Of(process), number, type, title, requestId, clock.UtcNow, currentUser.SubjectId, owningTeamId);

                workItems.Add(item);
                await workItems.SaveAsync(ct);

                return new CreateWorkItemResult(await readModel.GetAsync(item.Id, ct), Created: true);
            },
            cancellationToken);
    }

    /// <summary>
    /// The explicit team of a creation with no parent: «Без команды» is <c>null</c>; otherwise the team, which must be an
    /// active team of this project (api-contract § 0.2) — judged by <see cref="ProjectTeams.EnsureBindable"/>: the team on a
    /// fresh read, its link by <paramref name="linkHeld"/>, the answer of the statement that locked it. The fresh read's own
    /// <c>IsLinked</c> is not used: a link committed after the lock reads linked there and is held by nobody.
    /// </summary>
    private async Task<Guid?> DecideTeamAsync(
        TeamChange change, Guid organizationId, Guid projectId, bool linkHeld, CancellationToken cancellationToken)
    {
        if (change.TeamId is null)
        {
            return null;
        }

        if (!AddressedId.TryParse(change.TeamId, out var teamId))
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var found = await structure.FindProjectTeamAsync(organizationId, projectId, teamId, cancellationToken);

        ProjectTeams.EnsureBindable(
            found is null ? null : new TeamFacts(found.Team.Id, found.Team.OrganizationId, found.Team.Status, linkHeld),
            organizationId);

        return teamId;
    }

    /// <summary>The type of the organization's process with that id, or <c>null</c> for a malformed, unknown or foreign one.</summary>
    private static WorkItemTypeStates? TypeOf(ProcessDefinitionRow process, string? rawTypeId)
    {
        if (!AddressedId.TryParse(rawTypeId, out var typeId))
        {
            return null;
        }

        return ProcessWorkItemTypes.Find(process, typeId);
    }
}
