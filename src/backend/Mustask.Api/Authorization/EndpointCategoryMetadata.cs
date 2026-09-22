namespace Mustask.Api.Authorization;

public enum EndpointCategoryKind
{
    Permission,
    Public,
    Authenticated,
}

/// <summary>
/// Ровно одна такая метка обязана быть у каждого endpoint'а —
/// проверяется инвентаризационным тестом (`Mustask.Api.IntegrationTests`).
/// </summary>
public sealed class EndpointCategoryMetadata
{
    public EndpointCategoryKind Kind { get; }

    public string? PermissionCode { get; }

    public string? Reason { get; }

    private EndpointCategoryMetadata(EndpointCategoryKind kind, string? permissionCode, string? reason)
    {
        Kind = kind;
        PermissionCode = permissionCode;
        Reason = reason;
    }

    public static EndpointCategoryMetadata Permission(string code) =>
        new(EndpointCategoryKind.Permission, code, reason: null);

    public static EndpointCategoryMetadata Public(string reason) =>
        new(EndpointCategoryKind.Public, permissionCode: null, reason);

    public static EndpointCategoryMetadata Authenticated(string reason) =>
        new(EndpointCategoryKind.Authenticated, permissionCode: null, reason);
}
