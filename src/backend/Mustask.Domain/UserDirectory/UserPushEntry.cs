namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One user entry exactly as it arrived on the push channel, before
/// validation (api-contract.md, Part B, "The user entry"): every member may
/// be missing. <see cref="UserPushValidation"/> turns a list of these into
/// <see cref="UserPush"/>es or one refusal naming every bad field.
/// </summary>
public sealed record UserPushEntry(
    string? Id,
    long? Moment,
    string? Username,
    string? FirstName,
    string? LastName,
    string? Email,
    bool? Enabled,
    bool? Deleted);
