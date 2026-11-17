using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Imports.Source;

/// <summary>
/// The refusal of a whole file (BR-IE-2, BR-IE-4, BR-IE-21 and the file-safety NFR), raised before a row is read: a validation error
/// (400) whose top-level code is <see cref="ImportErrorCodes.FileRefused"/> and whose reason is <c>errors.file[0]</c>.
/// </summary>
public static class SourceRefusal
{
    /// <summary>The field the reason is keyed by.</summary>
    public const string Field = "file";

    /// <summary>Builds the refusal; throw the result.</summary>
    public static ValidationException Because(string reasonCode, ErrorParams? parameters = null, Exception? inner = null) =>
        new(
            ImportErrorCodes.FileRefused,
            new Dictionary<string, IReadOnlyList<FieldError>>
            {
                [Field] = [new FieldError(reasonCode, parameters ?? ErrorParams.None)],
            },
            inner);

    /// <summary>A bad source option (header row, first data row) as a field reason.</summary>
    public static ValidationException BadOption(string field, string reasonCode) =>
        new(
            ImportErrorCodes.ValidationFailed,
            new Dictionary<string, IReadOnlyList<FieldError>> { [field] = [new FieldError(reasonCode)] });
}
