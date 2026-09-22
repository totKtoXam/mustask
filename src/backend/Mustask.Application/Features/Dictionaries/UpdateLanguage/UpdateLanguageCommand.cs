namespace Mustask.Application.Features.Dictionaries.UpdateLanguage;

/// <summary>
/// `PUT /api/dictionaries/languages/{code}`.
/// This is a full replace: `SortOrder`/`IsActive` are nullable so the handler
/// can tell "absent from the body" from "sent as the zero value" and reject
/// the former with `dictionaries.value_required` rather than default
/// it — `Endonym` stays nullable by design: an absent/empty endonym is a
/// legal value (it normalizes to `""`, `Language.Replace`), only *activating*
/// with an empty endonym is a domain error.
/// </summary>
public sealed record UpdateLanguageCommand(
    string Code,
    string? Endonym,
    int? SortOrder,
    bool? IsActive,
    string? ExpectedVersion);
