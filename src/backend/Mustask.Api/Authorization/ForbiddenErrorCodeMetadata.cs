namespace Mustask.Api.Authorization;

/// <summary>
/// The error code an endpoint answers when its permission policy fails
/// (mod-002 D-p). Without it the platform answers an empty 403 that
/// <c>UseStatusCodePages</c> turns into <c>http.forbidden</c>; with it,
/// <see cref="ForbiddenErrorCodeResultHandler"/> throws a business 403 carrying
/// this code, rendered with its own catalog text. The policy stays the policy:
/// the endpoint remains <c>RequiresPermission</c> in the category inventory.
/// </summary>
public sealed record ForbiddenErrorCodeMetadata(string Code);

/// <summary>The extension that adds <see cref="ForbiddenErrorCodeMetadata"/>.</summary>
public static class ForbiddenErrorCodeEndpoints
{
    public static TBuilder WithForbiddenErrorCode<TBuilder>(this TBuilder builder, string code)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return builder.WithMetadata(new ForbiddenErrorCodeMetadata(code));
    }
}
