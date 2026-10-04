using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// The only way a push route takes its body (ADR-0025 § 3): a parameter type
/// with its own <see cref="BindAsync"/> that first checks what needs no body —
/// the two headers and the timestamp's window — then reads the raw body (at
/// most <see cref="PushBodyReader.MaxBodyBytes"/>), verifies its signature with
/// <see cref="PushSignatureVerifier"/>, and only then deserialises it. There
/// is no way to reach a handler with an unverified body; a test asserts every
/// push route binds its body through this type, so a fifth route cannot
/// forget the check.
/// </summary>
public sealed class SignedPushBody<T>
    where T : class
{
    private SignedPushBody(T value)
    {
        Value = value;
    }

    /// <summary>The verified, deserialised body.</summary>
    public T Value { get; }

    /// <summary>Minimal API's custom binding hook.</summary>
    public static async ValueTask<SignedPushBody<T>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);

        var verifier = context.RequestServices.GetRequiredService<PushSignatureVerifier>();

        // What needs no body comes first (security pass S3): both headers, well-formed, and the timestamp in its
        // window. A request that fails them is refused 401 before a byte of its body is read, however large it is.
        // They need no secret, so a request that passes them is still read — up to 4 MiB — before the signature
        // refuses it: the limit bounds the cost, it does not authenticate (security pass S6).
        verifier.EnsureEnvelopeValid(PushBodyReader.PartsOf(context, ReadOnlyMemory<byte>.Empty));

        var body = await PushBodyReader.ReadBoundedAsync(context.Request, context.RequestAborted);

        verifier.EnsureValid(PushBodyReader.PartsOf(context, body));

        T? value;

        try
        {
            value = JsonSerializer.Deserialize<T>(body.Span, PushBodyReader.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw PushBodyReader.Malformed(ex);
        }

        return new SignedPushBody<T>(value ?? throw PushBodyReader.Malformed(null));
    }
}

/// <summary>The non-generic half of <see cref="SignedPushBody{T}"/>.</summary>
internal static class PushBodyReader
{
    /// <summary>The push body limit (api-contract.md B.1, Part D): 4 MiB.</summary>
    public const int MaxBodyBytes = 4 * 1024 * 1024;

    /// <summary>The routes' base; a signature's target is the path below it.</summary>
    public const string FeedBasePath = "/api/user-directory/feed";

    /// <summary>camelCase names, case-insensitive, unknown properties ignored (B.1).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            throw Malformed(null);
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];

        while (true)
        {
            var read = await request.Body.ReadAsync(chunk, cancellationToken);

            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxBodyBytes)
            {
                throw Malformed(null);
            }

            buffer.Write(chunk, 0, read);
        }

        // The stream's own array, not a copy of it: disposing a MemoryStream releases nothing the slice depends on.
        return new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static PushRequestParts PartsOf(HttpContext context, ReadOnlyMemory<byte> body)
    {
        var request = context.Request;
        var path = RawPath(context);

        var target = path.StartsWith(FeedBasePath + "/", StringComparison.OrdinalIgnoreCase)
            ? path[(FeedBasePath.Length + 1)..]
            : path;

        var authorization = request.Headers.Authorization.ToString();

        return new PushRequestParts(
            request.Method,
            target,
            request.Path.Value ?? string.Empty,
            SingleHeader(request, PushSignatureVerifier.TimestampHeader),
            SingleHeader(request, PushSignatureVerifier.SignatureHeader),
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase),
            body);
    }

    public static ValidationException Malformed(Exception? innerException) =>
        new(
            UserDirectoryErrorCodes.PushMalformed,
            new Dictionary<string, IReadOnlyList<FieldError>>(),
            innerException);

    /// <summary>The path exactly as sent — the raw request target without its query — falling back to the escaped path.</summary>
    private static string RawPath(HttpContext context)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;

        if (string.IsNullOrEmpty(raw) || !raw.StartsWith('/'))
        {
            return (context.Request.PathBase + context.Request.Path).ToUriComponent();
        }

        var query = raw.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? raw : raw[..query];
    }

    /// <summary>A header given more than once is not of the declared form: none of its values is taken.</summary>
    private static string? SingleHeader(HttpRequest request, string name)
    {
        var values = request.Headers[name];

        return values.Count == 1 ? values[0] : values.Count == 0 ? null : string.Empty;
    }
}
