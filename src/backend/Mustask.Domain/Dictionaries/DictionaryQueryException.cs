using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Dictionaries;

/// <summary>
/// `dictionaries.invalid_page_size`, `dictionaries.unknown_query_field` and
/// `dictionaries.invalid_query_syntax` are the **top-level** `errorCode` of
/// the response, not a field reason nested under
/// `dictionaries.validation_failed`'s `errors`.
/// <see cref="Mustask.Shared.Exceptions.ValidationException"/> always renders
/// its code as `dictionaries.validation_failed` plus an `errors` map, which
/// is exactly the shape these codes must not have, so they need their own
/// <see cref="MustaskException"/> subclass instead. HTTP 400, same status a
/// validation failure would have used.
/// </summary>
public sealed class DictionaryQueryException : MustaskException
{
    public DictionaryQueryException(string code, ErrorParams? parameters = null, Exception? innerException = null)
        : base(code, statusCode: 400, parameters, innerException)
    {
    }
}
