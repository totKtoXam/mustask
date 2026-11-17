using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>The body of a template create or save (api-contract § 2.5).</summary>
public sealed record TemplateBody(string? Name, TemplateSettings? Settings);

/// <summary>The checks a template save shares between its slices: the shape, the targets and the name (api-contract § 2.5).</summary>
public static class ImportTemplateRules
{
    /// <summary>Shape and targets, every reason at once (400); then the name against the others (409). Call inside the template lock.</summary>
    public static async Task EnsureSavableAsync(
        TemplateBody body,
        IReadOnlyList<ImportTemplate> existing,
        Guid? selfId,
        Guid organizationId,
        IImportReferenceReader references,
        CancellationToken cancellationToken)
    {
        var errors = TemplateValidator.Validate(body.Name, body.Settings);

        if (body.Settings is not null)
        {
            var refs = await references.LoadAsync(organizationId, [], cancellationToken);
            TemplateTargetCheck.AddErrors(body.Settings, refs, errors);
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(
                ImportErrorCodes.ValidationFailed,
                errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<FieldError>)pair.Value));
        }

        var key = ImportTemplate.KeyOf(body.Name!);

        if (existing.Any(t => t.Id != selfId && t.NameKey == key))
        {
            throw new ConflictException(ImportErrorCodes.TemplateNameTaken);
        }
    }

    public static void EnsureRoom(IReadOnlyList<ImportTemplate> existing)
    {
        if (existing.Count >= ImportLimits.MaxTemplates)
        {
            throw new ConflictException(ImportErrorCodes.TemplateLimitReached);
        }
    }
}
