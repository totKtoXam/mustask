using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>Сущность не найдена или недоступна запрашивающему. HTTP 404.</summary>
public sealed class NotFoundException : MustaskException
{
    public NotFoundException(string code, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, 404, parameters, innerException)
    {
    }
}
