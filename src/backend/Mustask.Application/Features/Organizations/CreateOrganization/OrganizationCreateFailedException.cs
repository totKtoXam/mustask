using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// The process seed failed while an organization was being created (BR-M1-3,
/// status.md D-t): <see cref="OrganizationErrorCodes.OrganizationCreateFailed"/>,
/// HTTP 500. The failure is the server's, and US-1's text asks the user to
/// retry; a 409 would claim a conflict the client could fix. The seed's own
/// exception is the inner exception: it reaches the log, never the client. A
/// module-owned subclass has a precedent in <c>DictionaryQueryException</c>.
/// </summary>
public sealed class OrganizationCreateFailedException(Exception innerException)
    : MustaskException(OrganizationErrorCodes.OrganizationCreateFailed, statusCode: 500, parameters: null, innerException);
