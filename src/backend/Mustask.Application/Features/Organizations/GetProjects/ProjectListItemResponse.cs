namespace Mustask.Application.Features.Organizations.GetProjects;

/// <summary><c>ProjectListItem</c> of api-contract § 2.1. The slice's own copy of the shape.</summary>
public sealed record ProjectListItemResponse(Guid Id, string Name, string Key);
