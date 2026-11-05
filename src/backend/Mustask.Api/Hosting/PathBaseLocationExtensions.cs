namespace Mustask.Api.Hosting;

/// <summary>
/// ADR-0037: under a path base, a root-relative <c>Location</c> (the <c>Results.Created("/api/...")</c> of the
/// create endpoints) would point at the root of the host, outside the prefix. One place adds the prefix, instead of
/// every endpoint knowing it. Absolute addresses (the session redirects, built from <c>Session:PanelUrl</c>) and
/// requests that came without the prefix are left alone.
/// </summary>
public static class PathBaseLocationExtensions
{
    public static IApplicationBuilder UsePathBaseOnRelativeLocation(this IApplicationBuilder app) =>
        app.Use(static (context, next) =>
        {
            // Taken now: UsePathBase restores Request.PathBase when the pipeline unwinds, which can be before OnStarting runs.
            var pathBase = context.Request.PathBase;

            if (pathBase.HasValue)
            {
                context.Response.OnStarting(static state =>
                {
                    var (http, prefix) = ((HttpContext, string))state;
                    var location = http.Response.Headers.Location.ToString();

                    if (location.StartsWith('/') && !location.StartsWith("//", StringComparison.Ordinal))
                    {
                        http.Response.Headers.Location = prefix + location;
                    }

                    return Task.CompletedTask;
                }, (context, pathBase.Value!));
            }

            return next(context);
        });
}
