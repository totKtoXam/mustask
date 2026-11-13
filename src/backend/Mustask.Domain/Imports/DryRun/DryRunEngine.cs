using System.Globalization;
using Mustask.Domain.Imports.Conversion;
using Mustask.Domain.Imports.Source;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Imports.DryRun;

/// <summary>The result of a dry run: one entry per data row of the file, and the notes of the whole run.</summary>
public sealed record DryRunOutcome(IReadOnlyList<DryRunRowResult> Rows, IReadOnlyList<string> Notes);

/// <summary>
/// The dry run (doc-017 BR-IE-13): every data row is converted, recoded, matched by the MUSTASK identifier and checked as the form checks it
/// (BR-IE-12, within the scope plan-backend § 2 lists), and <b>nothing is written</b>. Pure over the table and the <see cref="DryRunContext"/>.
/// </summary>
public static class DryRunEngine
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The key two identifiers are compared by: trimmed and upper-cased (BR-IE-10).</summary>
    public static string NormalizeIdentifier(string identifier) => identifier.Trim().ToUpperInvariant();

    /// <summary>The header texts match ignoring case and edge spaces (BR-IE-4); the index of each, first wins.</summary>
    public static Dictionary<string, int> HeaderIndex(IEnumerable<SourceHeader> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            map.TryAdd(header.Text.Trim(), header.Index);
        }

        return map;
    }

    /// <summary>Headers the template maps that the file lacks (BR-IE-4): the whole file is refused naming them.</summary>
    public static IReadOnlyList<string> MissingColumns(TemplateSettings settings, IReadOnlyDictionary<string, int> headers)
    {
        var missing = new List<string>();

        foreach (var mapping in settings.Mappings)
        {
            var needed = mapping.Source.Kind switch
            {
                TemplateTokens.SourceColumn => [mapping.Source.Column ?? string.Empty],
                TemplateTokens.SourceMerge => ConverterEngine.MergePlaceholders(mapping.Source.Pattern ?? string.Empty).ToArray(),
                _ => Array.Empty<string>(),
            };

            foreach (var name in needed.Select(n => n.Trim()).Where(n => n.Length > 0 && !headers.ContainsKey(n)))
            {
                if (!missing.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    missing.Add(name);
                }
            }
        }

        return missing;
    }

    /// <summary>
    /// The identifier column of a template (BR-IE-10): the column of the mapping to «Идентификатор MUSTASK», else the file's «Идентификатор»
    /// column when the template leaves it unmapped and does not list it as ignored; <c>null</c> when there is none.
    /// </summary>
    public static int? IdentifierColumn(TemplateSettings settings, IReadOnlyDictionary<string, int> headers)
    {
        var mapping = settings.MappingOf(ImportFieldCatalogue.Identifier);

        if (mapping is not null)
        {
            return mapping.Source.Kind == TemplateTokens.SourceColumn && headers.TryGetValue((mapping.Source.Column ?? string.Empty).Trim(), out var mapped) ? mapped : null;
        }

        var name = ImportFieldCatalogue.ExportIdentifierHeader;
        var mappedElsewhere = settings.Mappings.Any(m => m.Source.Kind == TemplateTokens.SourceColumn && string.Equals((m.Source.Column ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
        var ignored = settings.IgnoredColumns.Any(c => string.Equals(c.Trim(), name, StringComparison.OrdinalIgnoreCase));

        return !mappedElsewhere && !ignored && headers.TryGetValue(name, out var index) ? index : null;
    }

    /// <summary>The cell as the start value of a chain (BR-IE-3, BR-IE-9).</summary>
    public static ConversionValue StartValue(SourceCell cell) => cell.Kind switch
    {
        SourceCellKind.Empty => ConversionValue.Empty,
        SourceCellKind.Number when cell.Number is { } n => ConversionValue.OfNumber((decimal)n),
        SourceCellKind.Date when cell.Date is { } d => ConversionValue.OfDate(DateOnly.FromDateTime(d)),
        _ => string.IsNullOrWhiteSpace(cell.Text) ? ConversionValue.Empty : ConversionValue.OfText(cell.Text),
    };

    /// <summary>One mapping evaluated on a row: the chain's result, a failure, and the cell it came from.</summary>
    public sealed record Evaluation(FieldMapping Mapping, ConversionValue Value, ConversionFailure? Failure, string? Column, string? Cell, string Original);

    /// <summary>Evaluates a mapping's source and chain on a row.</summary>
    public static Evaluation Evaluate(FieldMapping mapping, SourceRow row, IReadOnlyDictionary<string, int> headers, int? dateSystem)
    {
        SourceCell? cell = null;
        ConversionValue start;
        var original = string.Empty;

        switch (mapping.Source.Kind)
        {
            case TemplateTokens.SourceConstant:
                start = ConversionValue.OfText(mapping.Source.Value ?? string.Empty);
                return new(mapping, start, null, null, null, mapping.Source.Value ?? string.Empty);
            case TemplateTokens.SourceMerge:
                start = ConverterEngine.Merge(mapping.Source.Pattern ?? string.Empty, name => CellOf(row, headers, name)?.Text ?? string.Empty);
                return new(mapping, start, null, null, null, start.Display());
            default:
                cell = CellOf(row, headers, mapping.Source.Column ?? string.Empty);
                start = cell is null ? ConversionValue.Empty : StartValue(cell);
                original = cell?.Text ?? string.Empty;
                break;
        }

        var steps = mapping.Chain.Select(ChainParams.ToEngine)
            .Select(s => s.Converter == ConverterIds.ExcelDate && s.Format is null && dateSystem == 1904 ? s with { Format = "1904" } : s)
            .ToList();
        var result = ConverterEngine.RunChain(start, steps, cell?.Hyperlink);

        return new(mapping, result.Value, result.Failure, mapping.Source.Column?.Trim(), cell?.Address, original);
    }

    private static SourceCell? CellOf(SourceRow row, IReadOnlyDictionary<string, int> headers, string column) =>
        headers.TryGetValue(column.Trim(), out var index) && index < row.Cells.Count ? row.Cells[index] : null;

    /// <summary>Runs the dry run over all data rows.</summary>
    public static DryRunOutcome Run(TemplateSettings settings, SourceTable table, DryRunContext context)
    {
        var headers = HeaderIndex(table.Headers);
        var identifierColumn = IdentifierColumn(settings, headers);
        var notes = new List<string>();

        if (identifierColumn is null)
        {
            notes.Add(ImportErrorCodes.NoteNoIdentifierColumn);
        }

        if (table.Rows.Count == 0)
        {
            notes.Add(ImportErrorCodes.NoteEmptyFile);
        }

        var identifierMapping = settings.MappingOf(ImportFieldCatalogue.Identifier);
        var identifiers = new Dictionary<int, string>();

        foreach (var row in table.Rows)
        {
            var text = identifierColumn is { } c && c < row.Cells.Count
                ? (identifierMapping is not null ? Evaluate(identifierMapping, row, headers, table.DateSystem).Value.Display() : row.Cells[c].Text).Trim()
                : string.Empty;

            if (text.Length > 0)
            {
                identifiers[row.Number] = text;
            }
        }

        var duplicates = identifiers.GroupBy(pair => NormalizeIdentifier(pair.Value)).Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(pair => (row: pair.Key, others: g.Where(o => o.Key != pair.Key).Select(o => o.Key).ToList())))
            .ToDictionary(x => x.row, x => x.others);

        var results = new List<DryRunRowResult>(table.Rows.Count);

        foreach (var row in table.Rows)
        {
            identifiers.TryGetValue(row.Number, out var identifier);
            duplicates.TryGetValue(row.Number, out var others);
            results.Add(RunRow(settings, row, headers, table.DateSystem, identifier, others, context));
        }

        return new DryRunOutcome(results, notes);
    }

    private sealed class RowState(int number)
    {
        public int Number { get; } = number;

        public List<DryRunReason> Reasons { get; } = [];

        public bool HasError => Reasons.Any(r => r.Severity == "error");

        public void Error(string code, Dictionary<string, string>? parameters = null, string? column = null, string? cell = null, string? value = null) =>
            Reasons.Add(new DryRunReason { Severity = "error", Code = code, Params = parameters ?? [], Column = column, Cell = cell, Value = value });

        public void Warning(string code, Dictionary<string, string>? parameters = null, string? column = null, string? cell = null, string? value = null) =>
            Reasons.Add(new DryRunReason { Severity = "warning", Code = code, Params = parameters ?? [], Column = column, Cell = cell, Value = value });
    }

    private sealed record Resolved(string Field, string? Canonical, bool Clear, Evaluation Source);

    private static DryRunRowResult RunRow(
        TemplateSettings settings,
        SourceRow row,
        IReadOnlyDictionary<string, int> headers,
        int? dateSystem,
        string? identifier,
        IReadOnlyList<int>? duplicateRows,
        DryRunContext context)
    {
        var state = new RowState(row.Number);

        // BR-IE-3: a row whose mapped cells are all empty is skipped and counted.
        var evaluations = settings.Mappings.Select(m => Evaluate(m, row, headers, dateSystem)).ToList();
        var columnMappings = evaluations.Where(e => e.Mapping.Source.Kind == TemplateTokens.SourceColumn).ToList();

        if (row.IsEmpty || (columnMappings.Count > 0 && columnMappings.All(e => e.Failure is null && e.Value.IsEmpty)))
        {
            state.Warning(ImportErrorCodes.RowEmpty);
            return Result(state, RowResults.Skipped, null, identifier, null, []);
        }

        foreach (var e in evaluations.Where(e => e.Failure is not null))
        {
            if (e.Failure!.Timeout)
            {
                state.Error(ImportErrorCodes.RowRegexTimeout, null, e.Column, e.Cell, e.Original);
            }
            else
            {
                state.Error(ImportErrorCodes.RowConvertFailed, new() { ["column"] = e.Column ?? string.Empty, ["cell"] = e.Cell ?? string.Empty, ["value"] = e.Original, ["converter"] = e.Failure.Converter }, e.Column, e.Cell, e.Original);
            }
        }

        var byField = evaluations.Where(e => e.Failure is null).ToDictionary(e => e.Mapping.Field, e => e, StringComparer.Ordinal);

        // The identifier first: it decides create or update (BR-IE-10).
        DryRunItemRef? item = null;
        var emptyIdentifier = string.IsNullOrWhiteSpace(identifier);

        if (duplicateRows is { Count: > 0 })
        {
            state.Error(ImportErrorCodes.RowIdentifierDuplicate, new() { ["row"] = string.Join(", ", duplicateRows) });
        }
        else if (!emptyIdentifier)
        {
            if (!context.Items.TryGetValue(NormalizeIdentifier(identifier!), out item) || !context.CanRead(item.ProjectId))
            {
                item = null;
                state.Error(ImportErrorCodes.RowIdentifierNotFound, new() { ["identifier"] = identifier! });
            }
            else if (item.Deleted)
            {
                state.Error(ImportErrorCodes.RowIdentifierDeleted, new() { ["identifier"] = identifier! });
                item = null;
            }
        }
        else if (settings.Identifier.EmptyIdentifier == TemplateTokens.IdentifierSkip && IdentifierColumnPresent(settings, headers))
        {
            state.Warning(ImportErrorCodes.RowIdentifierSkipped);
            return Result(state, RowResults.Skipped, null, null, null, []);
        }

        if (state.HasError && !emptyIdentifier && item is null)
        {
            return Result(state, RowResults.Error, TitleOf(byField), identifier, null, []);
        }

        var isUpdate = item is not null;

        // Project and type.
        Guid? projectId = isUpdate ? item!.ProjectId : null;
        Guid? typeId = isUpdate ? item!.TypeId : null;

        if (byField.TryGetValue(ImportFieldCatalogue.Project, out var projectEval) && !projectEval.Value.IsEmpty)
        {
            var resolved = Recode(settings, state, projectEval, RecodingKind.Project, null);

            if (resolved is not null)
            {
                if (!Guid.TryParse(resolved, out var id) || !context.Projects.TryGetValue(id, out var project) || !project.Active)
                {
                    state.Error(projectEval.Mapping.Source.Kind == TemplateTokens.SourceConstant || settings.RecodingOf(projectEval.Mapping.Id) is null ? ImportErrorCodes.RowProjectNotFound : ImportErrorCodes.RowRecodingOutdated, new() { ["field"] = "Проект" }, projectEval.Column, projectEval.Cell, projectEval.Original);
                }
                else if (isUpdate && id != projectId)
                {
                    state.Error(ImportErrorCodes.RowProjectChanged, null, projectEval.Column, projectEval.Cell, projectEval.Original);
                }
                else
                {
                    projectId = id;
                }
            }
        }

        if (byField.TryGetValue(ImportFieldCatalogue.Type, out var typeEval) && !typeEval.Value.IsEmpty)
        {
            var resolved = Recode(settings, state, typeEval, RecodingKind.Type, null);

            if (resolved is not null)
            {
                if (!Guid.TryParse(resolved, out var id) || !context.Types.ContainsKey(id))
                {
                    state.Error(ImportErrorCodes.RowTypeNotFound, null, typeEval.Column, typeEval.Cell, typeEval.Original);
                }
                else if (isUpdate && id != typeId)
                {
                    state.Error(ImportErrorCodes.RowTypeChanged, null, typeEval.Column, typeEval.Cell, typeEval.Original);
                }
                else
                {
                    typeId = id;
                }
            }
        }

        var type = typeId is { } t && context.Types.TryGetValue(t, out var typeRef) ? typeRef : null;

        if (projectId is { } p && !context.CanImportInto(p))
        {
            state.Error(ImportErrorCodes.RowProjectForbidden);
        }

        if (!isUpdate)
        {
            if (projectId is null && !state.Reasons.Any(r => r.Code is ImportErrorCodes.RowProjectNotFound or ImportErrorCodes.RowRecodingOutdated or ImportErrorCodes.RowValueNotRecoded))
            {
                state.Error(ImportErrorCodes.RowRequiredMissing, new() { ["field"] = "Проект" });
            }

            if (type is null && !state.Reasons.Any(r => r.Code is ImportErrorCodes.RowTypeNotFound or ImportErrorCodes.RowValueNotRecoded))
            {
                state.Error(ImportErrorCodes.RowRequiredMissing, new() { ["field"] = "Тип" });
            }
        }

        // The other fields.
        var changes = new List<Resolved>();

        foreach (var eval in evaluations.Where(e => e.Failure is null))
        {
            var field = ImportFieldCatalogue.Find(eval.Mapping.Field);

            if (field is null || field.Id is ImportFieldCatalogue.Project or ImportFieldCatalogue.Type or ImportFieldCatalogue.Identifier)
            {
                continue;
            }

            var empty = eval.Value.IsEmpty;

            if (empty && (eval.Mapping.EmptyCell != TemplateTokens.EmptyClear || !isUpdate))
            {
                continue; // «Не менять»; on create both mean "not set" (BR-IE-6)
            }

            if (!empty && type is not null && !ImportFieldCatalogue.IsOnType(field, type.Kind))
            {
                if (eval.Mapping.FieldAbsent == TemplateTokens.AbsentDrop)
                {
                    state.Warning(ImportErrorCodes.RowFieldNotOnType, new() { ["field"] = field.Name }, eval.Column, eval.Cell, eval.Original);
                }
                else
                {
                    state.Error(ImportErrorCodes.RowFieldNotOnType, new() { ["field"] = field.Name }, eval.Column, eval.Cell, eval.Original);
                }

                continue;
            }

            var canonical = empty ? null : Canonical(settings, state, eval, field, type, projectId, context);

            if (!empty && canonical is null)
            {
                continue; // the reason is on the row
            }

            changes.Add(new Resolved(field.Id, canonical, empty, eval));
        }

        // Dates: the target is not before the start (the form's rule).
        var start = Pick(changes, ImportFieldCatalogue.StartDate) ?? (isUpdate ? item!.Values.GetValueOrDefault(ImportFieldCatalogue.StartDate) : null);
        var target = Pick(changes, ImportFieldCatalogue.TargetDate) ?? (isUpdate ? item!.Values.GetValueOrDefault(ImportFieldCatalogue.TargetDate) : null);

        if (DateOnly.TryParse(start, Invariant, out var s) && DateOnly.TryParse(target, Invariant, out var tg) && WorkItemFieldRules.TargetBeforeStart(s, tg))
        {
            state.Error(ImportErrorCodes.RowValueInvalid, new() { ["field"] = "Целевая дата" });
        }

        if (!isUpdate && !changes.Any(c => c.Field == ImportFieldCatalogue.Title && c.Canonical is not null))
        {
            if (!state.Reasons.Any(r => r.Code == ImportErrorCodes.RowValueInvalid && r.Params.GetValueOrDefault("field") == "Заголовок"))
            {
                state.Error(ImportErrorCodes.RowRequiredMissing, new() { ["field"] = "Заголовок" });
            }
        }

        var projectRef = projectId is { } pid && context.Projects.TryGetValue(pid, out var pr) ? new DryRunProject(pr.Id, pr.Name) : null;
        var title = Pick(changes, ImportFieldCatalogue.Title) ?? (isUpdate ? item!.Values.GetValueOrDefault(ImportFieldCatalogue.Title) : null);
        var shownIdentifier = isUpdate ? item!.Identifier : null;

        if (state.HasError)
        {
            return Result(state, RowResults.Error, title, shownIdentifier ?? identifier, projectRef, []);
        }

        if (!isUpdate)
        {
            var fields = changes.Where(c => c.Canonical is not null).Select(c => Field(c.Field, c.Canonical, null, context)).ToList();
            return Result(state, RowResults.Created, title, null, projectRef, fields);
        }

        var diff = new List<DryRunFieldValue>();

        foreach (var c in changes)
        {
            var old = item!.Values.GetValueOrDefault(c.Field);
            var now = c.Clear ? DefaultForClear(c.Field) : c.Canonical;

            if (!string.Equals(old ?? string.Empty, now ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                diff.Add(Field(c.Field, now, old ?? string.Empty, context));
            }
        }

        return Result(state, diff.Count == 0 ? RowResults.Unchanged : RowResults.Updated, title, shownIdentifier, projectRef, diff);
    }

    private static string? Pick(List<Resolved> changes, string field) => changes.FirstOrDefault(c => c.Field == field)?.Canonical;

    private static bool IdentifierColumnPresent(TemplateSettings settings, IReadOnlyDictionary<string, int> headers) =>
        IdentifierColumn(settings, headers) is not null;

    private static string? TitleOf(Dictionary<string, Evaluation> byField) =>
        byField.TryGetValue(ImportFieldCatalogue.Title, out var e) && !e.Value.IsEmpty ? ConverterEngine.ToFieldText(e.Value) : null;

    private static DryRunRowResult Result(RowState state, string result, string? title, string? identifier, DryRunProject? project, IReadOnlyList<DryRunFieldValue> fields) =>
        new() { Row = state.Number, Result = result, Title = title, Identifier = identifier, Project = project, Fields = fields, Reasons = state.Reasons };

    private static string? DefaultForClear(string field) => field switch
    {
        ImportFieldCatalogue.Priority => WorkItemFieldRules.DefaultPriority.ToString(Invariant),
        ImportFieldCatalogue.Severity => WorkItemFieldRules.DefaultSeverity.ToString(Invariant),
        ImportFieldCatalogue.ValueArea => WorkItemFieldRules.DefaultValueArea.ToString(),
        _ => null,
    };

    private static DryRunFieldValue Field(string field, string? value, string? was, DryRunContext context)
    {
        var definition = ImportFieldCatalogue.Find(field)!;
        return new(field, definition.Name, Display(field, value, context), was is null ? null : Display(field, was, context));
    }

    private static string? Display(string field, string? canonical, DryRunContext context)
    {
        if (string.IsNullOrEmpty(canonical))
        {
            return canonical;
        }

        return field switch
        {
            ImportFieldCatalogue.State => context.Types.Values.SelectMany(t => t.States).FirstOrDefault(s => s.Id.ToString() == canonical)?.Name ?? canonical,
            ImportFieldCatalogue.Assignee => context.Members.TryGetValue(canonical, out var m) ? m.Name : canonical,
            ImportFieldCatalogue.OwningTeam => Guid.TryParse(canonical, out var team) && context.Teams.TryGetValue(team, out var name) ? name : canonical,
            _ => canonical,
        };
    }

    /// <summary>The recoded target of a value, or <c>null</c> (with a reason on the row, or "leave empty").</summary>
    private static string? Recode(TemplateSettings settings, RowState state, Evaluation eval, RecodingKind kind, Guid? typeId)
    {
        if (eval.Mapping.Source.Kind == TemplateTokens.SourceConstant)
        {
            return eval.Value.Display();
        }

        var table = settings.RecodingOf(eval.Mapping.Id);
        var key = eval.Value.Kind == ConversionKind.List ? eval.Value.Display() : ConverterEngine.ToFieldText(eval.Value);
        key = key.Trim();

        var entries = table?.Entries.Where(e => string.Equals(e.FileValue.Trim(), key, StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
        var entry = (kind == RecodingKind.State && typeId is not null ? entries.FirstOrDefault(e => e.TypeId == typeId) : null)
            ?? entries.FirstOrDefault(e => e.TypeId is null)
            ?? (kind == RecodingKind.State ? null : entries.FirstOrDefault());

        if (entry is not null)
        {
            return entry.Target;
        }

        switch (table?.Policy)
        {
            case TemplateTokens.PolicyDefault when !string.IsNullOrWhiteSpace(table.DefaultValue):
                return table.DefaultValue;
            case TemplateTokens.PolicyEmpty:
                return null;
            default:
                state.Error(ImportErrorCodes.RowValueNotRecoded, new() { ["value"] = key }, eval.Column, eval.Cell, eval.Original);
                return null;
        }
    }

    /// <summary>A field's canonical text after recoding and the form's value rules; <c>null</c> after putting the reason on the row.</summary>
    private static string? Canonical(
        TemplateSettings settings, RowState state, Evaluation eval, ImportFieldDefinition field, DryRunTypeRef? type, Guid? projectId, DryRunContext context)
    {
        void Invalid(string? code = null) =>
            state.Error(code ?? ImportErrorCodes.RowValueInvalid, new() { ["field"] = field.Name }, eval.Column, eval.Cell, eval.Original);

        switch (field.Id)
        {
            case ImportFieldCatalogue.Title:
            {
                var text = ConverterEngine.ToFieldText(eval.Value).Trim();

                if (text.Length == 0 || text.Length > WorkItemLimits.TitleMaxLength)
                {
                    Invalid();
                    return null;
                }

                return text;
            }

            case ImportFieldCatalogue.Description:
            case ImportFieldCatalogue.ReproSteps:
            case ImportFieldCatalogue.SystemInfo:
            {
                var text = ConverterEngine.ToFieldText(eval.Value);

                if (text.Length > WorkItemFieldRules.TextMaxLength)
                {
                    Invalid();
                    return null;
                }

                return text;
            }

            case ImportFieldCatalogue.State:
            {
                var target = Recode(settings, state, eval, RecodingKind.State, type?.Id);

                if (target is null)
                {
                    return null;
                }

                var found = Guid.TryParse(target, out var id) ? type?.States.FirstOrDefault(s => s.Id == id && !s.Removed) : null;

                if (found is null)
                {
                    state.Error(ImportErrorCodes.RowStateNotFound, null, eval.Column, eval.Cell, eval.Original);
                    return null;
                }

                return found.Id.ToString();
            }

            case ImportFieldCatalogue.Assignee:
            {
                var target = Recode(settings, state, eval, RecodingKind.Assignee, null);

                if (target is null)
                {
                    return null;
                }

                if (!context.Members.TryGetValue(target, out var member) || !member.Active)
                {
                    state.Error(ImportErrorCodes.RowAssigneeNotFound, null, eval.Column, eval.Cell, eval.Original);
                    return null;
                }

                return member.Id;
            }

            case ImportFieldCatalogue.Priority:
            {
                var target = eval.Mapping.Source.Kind == TemplateTokens.SourceConstant || settings.RecodingOf(eval.Mapping.Id) is not null
                    ? Recode(settings, state, eval, RecodingKind.Priority, null)
                    : ConverterEngine.ToFieldText(eval.Value);

                if (target is null)
                {
                    return null;
                }

                if (!int.TryParse(target.Trim(), NumberStyles.None, Invariant, out var priority) || WorkItemFieldRules.ValidatePriority(priority) is not null)
                {
                    Invalid();
                    return null;
                }

                return priority.ToString(Invariant);
            }

            case ImportFieldCatalogue.OwningTeam:
            {
                var target = Recode(settings, state, eval, RecodingKind.Team, null);

                if (target is null)
                {
                    return null;
                }

                if (!Guid.TryParse(target, out var teamId) || projectId is not { } pid || !context.ProjectTeams.TryGetValue(pid, out var linked) || !linked.Contains(teamId))
                {
                    state.Error(ImportErrorCodes.RowTeamNotLinked, null, eval.Column, eval.Cell, eval.Original);
                    return null;
                }

                return teamId.ToString();
            }

            case ImportFieldCatalogue.Parent:
            {
                var text = ConverterEngine.ToFieldText(eval.Value).Trim();

                if (!context.Items.TryGetValue(NormalizeIdentifier(text), out var parent) || !context.CanRead(parent.ProjectId))
                {
                    state.Error(ImportErrorCodes.RowIdentifierNotFound, new() { ["identifier"] = text }, eval.Column, eval.Cell, eval.Original);
                    return null;
                }

                if (parent.Deleted)
                {
                    state.Error(ImportErrorCodes.RowIdentifierDeleted, new() { ["identifier"] = text }, eval.Column, eval.Cell, eval.Original);
                    return null;
                }

                return parent.Id.ToString();
            }
        }

        return field.ValueKind switch
        {
            FieldValueKind.Integer or FieldValueKind.Decimal => CanonicalNumber(eval, field, Invalid),
            FieldValueKind.Date => CanonicalDate(eval, Invalid),
            FieldValueKind.Key => CanonicalKey(eval, field, Invalid),
            _ => ConverterEngine.ToFieldText(eval.Value),
        };
    }

    private static string? CanonicalNumber(Evaluation eval, ImportFieldDefinition field, Action<string?> invalid)
    {
        decimal number;

        switch (eval.Value.Kind)
        {
            case ConversionKind.Number:
                number = eval.Value.Number;
                break;
            case ConversionKind.Text when decimal.TryParse(eval.Value.Text.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant, out var parsed):
                number = parsed;
                break;
            default:
                invalid(null);
                return null;
        }

        string? code = field.Id switch
        {
            ImportFieldCatalogue.Risk => number == decimal.Truncate(number) ? WorkItemFieldRules.ValidateRisk((int)number) : WorkItemErrorCodes.IntegerRequired,
            ImportFieldCatalogue.Severity => number == decimal.Truncate(number) ? WorkItemFieldRules.ValidateSeverity((int)number) : WorkItemErrorCodes.IntegerRequired,
            _ when field.ValueKind == FieldValueKind.Integer => WorkItemFieldRules.ValidateInteger(number),
            _ => WorkItemFieldRules.ValidateDecimal(number),
        };

        if (code is not null)
        {
            invalid(null);
            return null;
        }

        return number.ToString("0.##", Invariant);
    }

    private static string? CanonicalDate(Evaluation eval, Action<string?> invalid)
    {
        DateOnly date;

        switch (eval.Value.Kind)
        {
            case ConversionKind.Date:
                date = eval.Value.Date;
                break;
            case ConversionKind.Text when DateOnly.TryParseExact(eval.Value.Text.Trim(), ["yyyy-MM-dd", "dd.MM.yyyy"], Invariant, DateTimeStyles.None, out var parsed):
                date = parsed;
                break;
            default:
                invalid(null);
                return null;
        }

        if (WorkItemFieldRules.ValidateDate(date) is not null)
        {
            invalid(ImportErrorCodes.RowDateOutOfRange);
            return null;
        }

        return date.ToString("yyyy-MM-dd", Invariant);
    }

    private static string? CanonicalKey(Evaluation eval, ImportFieldDefinition field, Action<string?> invalid)
    {
        var text = ConverterEngine.ToFieldText(eval.Value).Trim();
        var names = field.Id == ImportFieldCatalogue.Activity ? Enum.GetNames<WorkItemActivity>() : Enum.GetNames<WorkItemValueArea>();
        var match = names.FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            invalid(null);
        }

        return match;
    }
}
