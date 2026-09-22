namespace Mustask.Api.Features.Session;

/// <summary>
/// <c>returnUrl</c> is accepted only as a local path — a scheme/host, or a
/// protocol-relative address (<c>//evil.example</c>, or <c>/\evil.example</c>,
/// which some browsers still parse as protocol-relative) would turn the
/// session's own redirects into an open redirect.
/// </summary>
public static class ReturnUrlValidator
{
    public static bool IsLocalPath(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.StartsWith("/\\", StringComparison.Ordinal)
        // A control character or anything outside ASCII
        // rides all the way to the "Location" response header once accepted
        // (the panel never sends one, but Kestrel rejecting a non-ASCII
        // header value would turn our own 400 into an unrelated 500) —
        // printable ASCII only, same restriction RFC 7230 already places on
        // header field values.
        && value.All(static c => c is >= (char)0x20 and <= (char)0x7E);
}
