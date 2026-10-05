using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AddTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (US-4 "A second team comes with its own area", "A team's name is unique
/// among the project's active teams", "A new team's area name must be free
/// under the root", "Two teams of one name at the same moment"; US-7 "The
/// deleted team's name is free, but its area's name is not"). A new team comes
/// with a child of the root, named after it, owned by it explicitly and set as
/// its default area (BR-M1-9, BR-M1-14).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in; rules about the
/// addressed object alone (none: the address ends at the project); the values
/// in the body; the references in the body (none: the team is new); the
/// uniqueness pre-checks, the team's name before the area's; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the root <c>FOR SHARE</c>, the new area's
/// parent, so that a project rename (which holds the root <c>FOR UPDATE</c>)
/// and this insert serialize. The project is not locked: a write beneath only
/// reads its containers' status (ADR-0026 rule 8). Two teams of one name are
/// closed by the unique indexes, the team's first and the area's second, in the
/// one transaction (plan-backend § 8.1); the loser gets
/// <see cref="OrganizationErrorCodes.TeamNameTaken"/> from the repository's
/// constraint map.
/// </para>
/// </remarks>
public sealed class AddTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<AddTeamResult> Handle(AddTeamCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The organization-context filter has answered for the
                // organization already; the handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. Rules about the addressed object alone: none, the address ends at the project.

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none, the team is new. The one row this write binds to is the root, the
                // new area's parent, locked FOR SHARE.
                var root = await areas.LockRootAsync(projectId, forUpdate: false, ct);

                // 5. The uniqueness pre-checks. The team's name is unique among the project's active teams, so a
                // deleted team's name is free (BR-M1-1, ADR-0026 rule 6). The area's name must be free among the
                // root's children (BR-M1-9, BR-M1-12), and a deleted team's area counts until it is renamed or
                // deleted (US-7). The team's check answers first: an active team's own area holds the same name.
                if (await teams.IsActiveNameTakenAsync(projectId, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamNameTaken);
                }

                if (await areas.IsSiblingNameTakenAsync(root.Id, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaNameTaken);
                }

                var created = Team.CreateWithArea(projectId, name, root);
                teams.Add(created);

                // 6. Save. The team row goes first and the area row second (the area's owner key points at the team).
                // A concurrent addition of the same name that committed first fails the team insert on the team index,
                // and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await teams.SaveAsync(ct);

                return new AddTeamResult(Respond(created, root));
            },
            cancellationToken);

    /// <summary>
    /// The shape every team response uses (api-contract § 3). The default area's path comes from the one definition
    /// of a path, <see cref="AreaTree"/>: the new area is a child of the root, so it is its own name below the root.
    /// </summary>
    private static TeamResponse Respond(NewTeam created, Area root)
    {
        var tree = new AreaTree(
        [
            new AreaNode(root.Id, null, root.Name, root.OwnerTeamId, root.DefaultOfTeamId),
            new AreaNode(created.Area.Id, root.Id, created.Area.Name, created.Area.OwnerTeamId, created.Area.DefaultOfTeamId),
        ]);

        return new TeamResponse(
            created.Team.Id,
            created.Team.ProjectId,
            created.Team.Name,
            created.Team.IsFirstTeam,
            new TeamDefaultAreaResponse(created.Area.Id, tree.PathOf(created.Area.Id)));
    }
}
