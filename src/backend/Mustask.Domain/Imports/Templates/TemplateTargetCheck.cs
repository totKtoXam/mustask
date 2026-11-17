using Mustask.Domain.Imports.DryRun;
using Mustask.Shared.Errors;

namespace Mustask.Domain.Imports.Templates;

/// <summary>
/// The checks of a template that need the organization's data (BR-IE-7): every recoding target and every constant of a reference field must
/// be something that exists and still qualifies (an active project, a type, a state of the type outside «Удалена», an active member, a team).
/// </summary>
public static class TemplateTargetCheck
{
    public static void AddErrors(TemplateSettings settings, ImportReferences references, Dictionary<string, List<FieldError>> errors)
    {
        for (var i = 0; i < settings.Recoding.Count; i++)
        {
            var table = settings.Recoding[i];
            var field = settings.Mappings.Where(m => m.Id == table.MappingId).Select(m => ImportFieldCatalogue.Find(m.Field)).FirstOrDefault();

            if (field is null || field.Recoding == RecodingKind.None)
            {
                continue;
            }

            var bad = table.Entries.Any(e => !IsTarget(field.Recoding, e.Target, e.TypeId, references))
                || (table.Policy == TemplateTokens.PolicyDefault && !IsTarget(field.Recoding, table.DefaultValue, null, references));

            if (bad)
            {
                Add(errors, $"settings.recoding[{i}]", ImportErrorCodes.RecodingTargetUnknown);
            }
        }

        for (var i = 0; i < settings.Mappings.Count; i++)
        {
            var mapping = settings.Mappings[i];
            var field = ImportFieldCatalogue.Find(mapping.Field);

            if (field is { Recoding: not RecodingKind.None } && mapping.Source.Kind == TemplateTokens.SourceConstant
                && !IsTarget(field.Recoding, mapping.Source.Value, null, references))
            {
                Add(errors, $"settings.mappings[{i}].source", ImportErrorCodes.RecodingTargetUnknown);
            }
        }
    }

    private static bool IsTarget(RecodingKind kind, string? target, Guid? typeId, ImportReferences references)
    {
        if (!TemplateValidator.ValidTargetShape(kind, target))
        {
            return false;
        }

        switch (kind)
        {
            case RecodingKind.Priority:
                return true;
            case RecodingKind.Assignee:
                return references.Members.TryGetValue(target!, out var member) && member.Active;
        }

        var id = Guid.Parse(target!);

        return kind switch
        {
            RecodingKind.Project => references.Projects.TryGetValue(id, out var project) && project.Active,
            RecodingKind.Type => references.Types.ContainsKey(id),
            RecodingKind.Team => references.Teams.ContainsKey(id),
            RecodingKind.State => (typeId is { } t
                ? references.Types.Where(p => p.Key == t).Select(p => p.Value)
                : references.Types.Values).Any(type => type.States.Any(s => s.Id == id && !s.Removed)),
            _ => false,
        };
    }

    private static void Add(Dictionary<string, List<FieldError>> errors, string key, string code)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            errors[key] = list = [];
        }

        list.Add(new FieldError(code));
    }
}
