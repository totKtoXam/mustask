using Mustask.Shared.Exceptions;

namespace Mustask.Api.Features.Session;

/// <summary>
/// <c>returnUrl</c> failed <see cref="ReturnUrlValidator.IsLocalPath"/> — a
/// fixed 400, not <see cref="ValidationException"/>: this is a single
/// top-level <c>errorCode</c>, not a field-level reason under
/// <c>session.validation_failed</c>, which does not exist (same shape as
/// <c>DictionaryQueryException</c>).
/// </summary>
public sealed class InvalidReturnUrlException()
    : MustaskException(SessionErrorCodes.InvalidReturnUrl, StatusCodes.Status400BadRequest);
