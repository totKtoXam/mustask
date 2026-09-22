using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>
/// Состояние сущности не допускает операцию: нарушен инвариант, повторная
/// отправка, гонка. HTTP 409.
/// </summary>
public sealed class ConflictException : MustaskException
{
    public ConflictException(string code, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, 409, parameters, innerException)
    {
    }
}
