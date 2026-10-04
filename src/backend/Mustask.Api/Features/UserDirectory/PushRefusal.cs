namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// Why a push request was refused — logged, never answered: every one of
/// them is the same <c>401 user_directory.push_unauthorized</c> to the caller
/// (api-contract.md B.2).
/// </summary>
public enum PushRefusal
{
    /// <summary>No signature headers at all.</summary>
    MissingHeaders,

    /// <summary>A Bearer token where a signature belongs (BR-UP-10: a Keycloak token is not a credential).</summary>
    BearerInsteadOfSignature,

    /// <summary>A header present but not of the declared form.</summary>
    MalformedHeader,

    /// <summary>The timestamp is more than the window away from MUSTASK's clock.</summary>
    StaleTimestamp,

    /// <summary>The signature matches neither the secret nor the previous secret.</summary>
    WrongSignature,
}
