using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>
/// The service cannot take this request now but may later: a bounded resource is full (feat-078: the image decode
/// gate). HTTP 503. <see cref="RetryAfter"/> becomes the <c>Retry-After</c> header; the code and its text come from the
/// catalog like any business error.
/// </summary>
public sealed class ServiceUnavailableException : MustaskException
{
    public ServiceUnavailableException(string code, TimeSpan retryAfter, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, 503, parameters, innerException)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the client should wait before asking again.</summary>
    public TimeSpan RetryAfter { get; }
}
