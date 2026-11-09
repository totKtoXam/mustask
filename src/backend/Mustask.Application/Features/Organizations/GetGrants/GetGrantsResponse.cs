using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Organizations.GetGrants;

/// <summary><c>GrantList</c> of api-contract § 1: the rows in display order and, for a project under a department, the node's switch.</summary>
public sealed record GetGrantsResponse(IReadOnlyList<GrantRowView> Items, GrantInheritanceView? Inheritance);
