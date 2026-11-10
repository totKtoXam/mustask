using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>The department named on an inherited row or in the list's <c>inheritance</c> (api-contract § 1).</summary>
public sealed record DepartmentRefView(Guid Id, string Name);

/// <summary><c>GrantRow</c> of api-contract § 1.</summary>
public sealed record GrantRowView(
    UserRefView User,
    string Role,
    string Origin,
    DepartmentRefView? Department,
    UserRefView? GivenBy,
    DateTimeOffset GivenAt);

/// <summary>The project list's <c>inheritance</c> block (api-contract § 1).</summary>
public sealed record GrantInheritanceView(Guid DepartmentId, string DepartmentName, bool InheritsAccess);

/// <summary>The values of <see cref="GrantRowView.Origin"/>.</summary>
public static class GrantOrigins
{
    public const string Held = "held";

    public const string Department = "department";
}

/// <summary>Builds rows from stored grants, in the order of api-contract § 1 — shared by the read and the writes, which answer with a row.</summary>
public static class GrantRows
{
    public static GrantRowView Of(
        GrantRecord grant, string origin, DepartmentRefView? department, IReadOnlyDictionary<string, DirectoryUser> known) =>
        new(
            UserRefView.Of(grant.UserId, known),
            GrantRoles.ToCode(grant.Role),
            origin,
            department,
            // A giver the projection does not know is still named (as unknown): the id is what was recorded.
            UserRefView.Of(grant.GrantedBy, known),
            grant.GrantedAt);

    /// <summary>By display name (the people-order of mod-016), then the strongest role, then the held row before the inherited one.</summary>
    public static IReadOnlyList<GrantRowView> Order(IEnumerable<GrantRowView> rows, IReadOnlyDictionary<string, DirectoryUser> known)
    {
        var strength = GrantRoles.All.Select((role, index) => (Code: GrantRoles.ToCode(role), index)).ToDictionary(pair => pair.Code, pair => pair.index);
        var list = rows.ToList();
        var byPerson = UserRefOrder.Order(list.Select(row => row.User.Id).Distinct(StringComparer.Ordinal), id => id, known);
        var position = byPerson.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);

        return list
            .OrderBy(row => position[row.User.Id])
            .ThenBy(row => strength[row.Role])
            .ThenBy(row => row.Origin == GrantOrigins.Held ? 0 : 1)
            .ToList();
    }
}

/// <summary>What a grant route answers for an object that is not there, by scope (api-contract § 2).</summary>
public static class GrantObjects
{
    public static string NotFoundCodeOf(ResourceScope scope) => scope switch
    {
        ResourceScope.Area => OrganizationErrorCodes.AreaNotFound,
        ResourceScope.Project => OrganizationErrorCodes.ProjectNotFound,
        ResourceScope.Team => OrganizationErrorCodes.TeamNotFound,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a grant scope."),
    };
}

/// <summary>
/// The value checks both grant writes make, in one place (api-contract § 2, order of answers): the 400 on <c>userId</c> and
/// <c>role</c>, every reason at once, then the self-grant refusal of BR-SA-19 (only for a well-formed user id).
/// </summary>
public static class GrantInput
{
    public static (string UserId, GrantRole Role) Validate(string? userId, string? roleText, string actor)
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>();
        var userValid = OrganizationMembership.IsValidUserId(userId);

        if (!userValid)
        {
            errors["userId"] = [new FieldError(OrganizationErrorCodes.MembershipUserInvalid)];
        }

        if (!GrantRoles.TryParse(roleText, out var role))
        {
            errors["role"] = [new FieldError(OrganizationErrorCodes.GrantRoleInvalid)];
        }

        if (errors.Count == 0 && GrantRules.IsSelfGrant(userId!, actor))
        {
            // BR-SA-19: refused with the validation code, the administrator's own grant included.
            errors["userId"] = [new FieldError(OrganizationErrorCodes.GrantSelfGrant)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(OrganizationErrorCodes.ValidationFailed, errors);
        }

        return (userId!, role);
    }
}
