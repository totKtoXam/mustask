using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>Клиент не аутентифицирован. HTTP 401.</summary>
public sealed class UnauthorizedException : MustaskException
{
    public UnauthorizedException(string code, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, 401, parameters, innerException)
    {
    }
}
