using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>Клиент аутентифицирован, но прав на операцию не хватает. HTTP 403.</summary>
public sealed class ForbiddenException : MustaskException
{
    public ForbiddenException(string code, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, 403, parameters, innerException)
    {
    }
}
