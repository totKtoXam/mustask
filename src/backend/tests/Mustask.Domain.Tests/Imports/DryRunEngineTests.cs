using System.Text.Json;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Source;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Process;
using Xunit;

namespace Mustask.Domain.Tests.Imports;

public sealed class DryRunEngineTests
{
    private static readonly Guid PlatformId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ClosedProjectId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid OtherProjectId = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid TaskTypeId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid EpicTypeId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid TodoId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid DoneId = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
    private static readonly Guid RemovedId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");

    private static DryRunContext Context(params DryRunItemRef[] items) => new()
    {
        Projects = new Dictionary<Guid, DryRunProjectRef>
        {
            [PlatformId] = new(PlatformId, "Платформа", true),
            [ClosedProjectId] = new(ClosedProjectId, "Архив", false),
            [OtherProjectId] = new(OtherProjectId, "Закрытый", true),
        },
        Types = new Dictionary<Guid, DryRunTypeRef>
        {
            [TaskTypeId] = new(TaskTypeId, "Задача", AppearanceToken.Task, [new(TodoId, "К выполнению", false), new(DoneId, "Готово", false), new(RemovedId, "Удалена", true)]),
            [EpicTypeId] = new(EpicTypeId, "Эпик", AppearanceToken.Epic, [new(TodoId, "К выполнению", false)]),
        },
        Members = new Dictionary<string, DryRunMemberRef> { ["user-1"] = new("user-1", "Анна Тестова", true), ["user-2"] = new("user-2", "Бывший", false) },
        Teams = new Dictionary<Guid, string>(),
        ProjectTeams = new Dictionary<Guid, IReadOnlySet<Guid>>(),
        Items = items.ToDictionary(i => DryRunEngine.NormalizeIdentifier(i.Identifier)),
        CanImportInto = id => id != OtherProjectId,
        CanRead = _ => true,
        CanMemberRead = (_, _) => true,
    };

    private static DryRunItemRef Item(string identifier, string title = "Старое", bool deleted = false, Guid? project = null, Guid? type = null) =>
        new(Guid.NewGuid(), identifier, project ?? PlatformId, type ?? TaskTypeId, deleted,
            new Dictionary<string, string?> { ["title"] = title, ["state"] = TodoId.ToString(), ["priority"] = "2" });

    private static SourceTable Table(string[] headers, params string[][] rows) => new(
        SourceFormat.Csv, ["s"], null, 1, 2, "utf-8", ";", false, false, null,
        headers.Select((h, i) => new SourceHeader(i, h, CellAddress.Of(i, 1))).ToList(),
        rows.Select((r, n) => new SourceRow(n + 2, r.Select((c, i) => SourceCell.OfText(c, CellAddress.Of(i, n + 2))).ToList())).ToList(),
        rows.Length);

    private static FieldMapping Col(string field, string column, params ChainStepSettings[] chain) =>
        new() { Id = Guid.NewGuid(), Field = field, Source = new MappingSource { Kind = TemplateTokens.SourceColumn, Column = column }, Chain = chain };

    private static FieldMapping Const(string field, string value) =>
        new() { Id = Guid.NewGuid(), Field = field, Source = new MappingSource { Kind = TemplateTokens.SourceConstant, Value = value } };

    private static ChainStepSettings Step(string converter, string json = "{}") =>
        new() { Converter = converter, Params = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) };

    private static TemplateSettings Settings(params FieldMapping[] extra) => new()
    {
        Mappings = [Const("project", PlatformId.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок"), .. extra],
    };

    private static DryRunRowResult Single(TemplateSettings settings, SourceTable table, DryRunContext context) => Assert.Single(DryRunEngine.Run(settings, table, context).Rows);

    private static bool Has(DryRunRowResult row, string code) => row.Reasons.Any(r => r.Code == code);

    [Fact]
    public void Run_NewRowWithoutIdentifierColumn_IsCreated_AndNotesTheMissingColumn()
    {
        var outcome = DryRunEngine.Run(Settings(), Table(["Заголовок"], ["Подготовить отчёт"]), Context());

        var row = Assert.Single(outcome.Rows);
        Assert.Equal(RowResults.Created, row.Result);
        Assert.Equal("Подготовить отчёт", row.Title);
        Assert.Equal("Платформа", row.Project!.Name);
        Assert.Contains(ImportErrorCodes.NoteNoIdentifierColumn, outcome.Notes);
    }

    [Fact]
    public void Run_EmptyRow_IsSkippedWithTheReason()
    {
        var row = Single(Settings(), Table(["Заголовок"], [""]), Context());

        Assert.Equal(RowResults.Skipped, row.Result);
        Assert.True(Has(row, ImportErrorCodes.RowEmpty));
    }

    [Fact]
    public void Run_EmptyFile_NotesIt()
    {
        var outcome = DryRunEngine.Run(Settings(), Table(["Заголовок"]), Context());

        Assert.Empty(outcome.Rows);
        Assert.Contains(ImportErrorCodes.NoteEmptyFile, outcome.Notes);
    }

    [Fact]
    public void Run_ExportsIdentifierColumnByDefault_UpdatesTheMatchedItem_WithOldAndNew()
    {
        var table = Table(["Идентификатор", "Заголовок"], ["plt-task-1 ", "Новое"]);
        var row = Single(Settings(), table, Context(Item("PLT-TASK-1")));

        Assert.Equal(RowResults.Updated, row.Result);
        var field = Assert.Single(row.Fields);
        Assert.Equal(("title", "Новое", "Старое"), (field.Field, field.Value, field.Was));
    }

    [Fact]
    public void Run_IdentifierColumnSetToIgnored_IsNotUsed_RowCreates()
    {
        var settings = Settings() with { IgnoredColumns = ["Идентификатор"] };
        var row = Single(settings, Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Новое"]), Context(Item("PLT-TASK-1")));

        Assert.Equal(RowResults.Created, row.Result);
    }

    [Fact]
    public void Run_RowEqualToItsItem_IsUnchanged()
    {
        var row = Single(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Старое"]), Context(Item("PLT-TASK-1")));

        Assert.Equal(RowResults.Unchanged, row.Result);
        Assert.Empty(row.Fields);
    }

    [Fact]
    public void Run_UnknownIdentifier_IsAnErrorAndNeverCreates()
    {
        var row = Single(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-99", "Новое"]), Context());

        Assert.Equal(RowResults.Error, row.Result);
        Assert.True(Has(row, ImportErrorCodes.RowIdentifierNotFound));
    }

    [Fact]
    public void Run_DeletedItem_IsAnError()
    {
        var row = Single(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Новое"]), Context(Item("PLT-TASK-1", deleted: true)));

        Assert.True(Has(row, ImportErrorCodes.RowIdentifierDeleted));
    }

    [Fact]
    public void Run_EmptyIdentifier_CreatesOrSkipsBySetting()
    {
        var table = Table(["Идентификатор", "Заголовок"], ["", "Новое"]);

        Assert.Equal(RowResults.Created, Single(Settings(), table, Context()).Result);

        var skip = Settings() with { Identifier = new IdentifierSettings { EmptyIdentifier = TemplateTokens.IdentifierSkip } };
        var skipped = Single(skip, table, Context());
        Assert.Equal(RowResults.Skipped, skipped.Result);
        Assert.True(Has(skipped, ImportErrorCodes.RowIdentifierSkipped));
    }

    [Fact]
    public void Run_SameIdentifierTwice_BothAreErrorsNamingEachOther()
    {
        var table = Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "А"], ["plt-task-1", "Б"]);
        var rows = DryRunEngine.Run(Settings(), table, Context(Item("PLT-TASK-1"))).Rows;

        Assert.All(rows, r => Assert.True(Has(r, ImportErrorCodes.RowIdentifierDuplicate)));
        Assert.Equal("3", rows[0].Reasons.Single(r => r.Code == ImportErrorCodes.RowIdentifierDuplicate).Params["row"]);
        Assert.Equal("2", rows[1].Reasons.Single(r => r.Code == ImportErrorCodes.RowIdentifierDuplicate).Params["row"]);
    }

    [Fact]
    public void Run_UpdateThatChangesTypeOrProject_IsAnError()
    {
        var settings = new TemplateSettings { Mappings = [Const("project", PlatformId.ToString()), Const("type", EpicTypeId.ToString()), Col("title", "Заголовок")] };
        var row = Single(settings, Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Старое"]), Context(Item("PLT-TASK-1")));

        Assert.True(Has(row, ImportErrorCodes.RowTypeChanged));
    }

    [Fact]
    public void Run_ProjectTheImporterCannotImportInto_IsProjectForbidden()
    {
        var settings = new TemplateSettings { Mappings = [Const("project", OtherProjectId.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок")] };
        var row = Single(settings, Table(["Заголовок"], ["А"]), Context());

        Assert.Equal(RowResults.Error, row.Result);
        Assert.True(Has(row, ImportErrorCodes.RowProjectForbidden));
    }

    [Fact]
    public void Run_DeactivatedProject_IsProjectNotFound()
    {
        var settings = new TemplateSettings { Mappings = [Const("project", ClosedProjectId.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок")] };

        Assert.True(Has(Single(settings, Table(["Заголовок"], ["А"]), Context()), ImportErrorCodes.RowProjectNotFound));
    }

    [Fact]
    public void Run_RecodingPolicies_Error_Default_Empty()
    {
        var state = Col("state", "Статус");

        RecodingTableSettings Table_(string policy, string? def = null) => new()
        {
            MappingId = state.Id,
            Policy = policy,
            DefaultValue = def,
            Entries = [new RecodingEntrySettings { FileValue = "Сделано", Target = DoneId.ToString() }],
        };

        var data = Table(["Заголовок", "Статус"], ["А", " сделано "], ["Б", "Неизвестно"]);

        var strict = DryRunEngine.Run(Settings(state) with { Recoding = [Table_("error")] }, data, Context()).Rows;
        Assert.Equal(RowResults.Created, strict[0].Result);
        Assert.Contains(strict[0].Fields, f => f.Field == "state" && f.Value == "Готово");
        Assert.True(Has(strict[1], ImportErrorCodes.RowValueNotRecoded));

        var withDefault = DryRunEngine.Run(Settings(state) with { Recoding = [Table_("default", TodoId.ToString())] }, data, Context()).Rows;
        Assert.Contains(withDefault[1].Fields, f => f.Field == "state" && f.Value == "К выполнению");

        var empty = DryRunEngine.Run(Settings(state) with { Recoding = [Table_("empty")] }, data, Context()).Rows;
        Assert.DoesNotContain(empty[1].Fields, f => f.Field == "state");
    }

    [Fact]
    public void Run_StateTarget_InRemovedCategory_IsStateNotFound()
    {
        var state = Col("state", "Статус");
        var settings = Settings(state) with
        {
            Recoding = [new RecodingTableSettings { MappingId = state.Id, Entries = [new RecodingEntrySettings { FileValue = "Удалён", Target = RemovedId.ToString() }] }],
        };

        Assert.True(Has(Single(settings, Table(["Заголовок", "Статус"], ["А", "Удалён"]), Context()), ImportErrorCodes.RowStateNotFound));
    }

    [Fact]
    public void Run_ConverterFailure_NamesColumnCellValueAndConverter()
    {
        var settings = Settings(Col("storyPoints", "Оценка", Step("number")));
        var row = Single(settings, Table(["Заголовок", "Оценка"], ["А", "abc"]), Context());

        Assert.Equal(RowResults.Error, row.Result);
        var reason = row.Reasons.Single(r => r.Code == ImportErrorCodes.RowConvertFailed);
        Assert.Equal(("Оценка", "B2", "abc"), (reason.Column, reason.Cell, reason.Value));
        Assert.Equal("number", reason.Params["converter"]);
    }

    [Fact]
    public void Run_FieldNotOnType_IsErrorOrDroppedWarning()
    {
        // «Целевая дата» is carried by epics and features, not by tasks.
        var target = Col("targetDate", "Срок");
        var table = Table(["Заголовок", "Срок"], ["А", "2026-03-15"]);

        var error = Single(Settings(target), table, Context());
        Assert.Equal(RowResults.Error, error.Result);
        Assert.True(Has(error, ImportErrorCodes.RowFieldNotOnType));

        var dropped = Single(Settings(target with { FieldAbsent = TemplateTokens.AbsentDrop }), table, Context());
        Assert.Equal(RowResults.Created, dropped.Result);
        Assert.Contains(dropped.Reasons, r => r is { Code: ImportErrorCodes.RowFieldNotOnType, Severity: "warning" });
    }

    [Fact]
    public void Run_DateOutsideTheRange_IsDateOutOfRange()
    {
        var epic = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), Const("type", EpicTypeId.ToString()), Col("title", "Заголовок"), Col("targetDate", "Срок")],
        };

        Assert.True(Has(Single(epic, Table(["Заголовок", "Срок"], ["А", "1999-12-31"]), Context()), ImportErrorCodes.RowDateOutOfRange));
    }

    [Fact]
    public void Run_TargetBeforeStart_IsInvalid()
    {
        var epic = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), Const("type", EpicTypeId.ToString()), Col("title", "Заголовок"), Col("startDate", "С"), Col("targetDate", "По")],
        };

        Assert.True(Has(Single(epic, Table(["Заголовок", "С", "По"], ["А", "2026-05-01", "2026-04-01"]), Context()), ImportErrorCodes.RowValueInvalid));
    }

    [Fact]
    public void Run_EmptyCell_KeepsOrClearsByMapping_OnUpdate()
    {
        var table = Table(["Идентификатор", "Заголовок", "Приоритет"], ["PLT-TASK-1", "Старое", ""]);
        var keep = Single(Settings(Col("priority", "Приоритет")), table, Context(Item("PLT-TASK-1")));
        Assert.Equal(RowResults.Unchanged, keep.Result);

        var item = Item("PLT-TASK-1");
        var clearing = Col("priority", "Приоритет") with { EmptyCell = TemplateTokens.EmptyClear };
        var clear = Single(Settings(clearing), Table(["Идентификатор", "Заголовок", "Приоритет"], ["PLT-TASK-1", "Старое", ""]), Context(item with { Values = new Dictionary<string, string?>(item.Values) { ["priority"] = "1" } }));
        Assert.Equal(RowResults.Updated, clear.Result);
        Assert.Contains(clear.Fields, f => f.Field == "priority" && f.Value == "2" && f.Was == "1");
    }

    [Fact]
    public void Run_Assignee_InactiveOrUnknown_IsAssigneeNotFound()
    {
        var assignee = Col("assignee", "Кто");
        var settings = Settings(assignee) with
        {
            Recoding =
            [
                new RecodingTableSettings
                {
                    MappingId = assignee.Id,
                    Entries =
                    [
                        new RecodingEntrySettings { FileValue = "Анна", Target = "user-1" },
                        new RecodingEntrySettings { FileValue = "Бывший", Target = "user-2" },
                    ],
                },
            ],
        };
        var rows = DryRunEngine.Run(settings, Table(["Заголовок", "Кто"], ["А", "Анна"], ["Б", "Бывший"]), Context()).Rows;

        Assert.Equal(RowResults.Created, rows[0].Result);
        Assert.True(Has(rows[1], ImportErrorCodes.RowAssigneeNotFound));
    }

    [Fact]
    public void Run_TitleTooLong_IsValueInvalid_AndTruncateStepFixesIt()
    {
        var long_ = new string('я', 256);

        Assert.True(Has(Single(Settings(), Table(["Заголовок"], [long_]), Context()), ImportErrorCodes.RowValueInvalid));

        var settings = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок", Step("truncate", "{\"length\":255}"))],
        };
        Assert.Equal(RowResults.Created, Single(settings, Table(["Заголовок"], [long_]), Context()).Result);
    }

    [Fact]
    public void Run_Writes_NothingAndTheContextIsUntouched()
    {
        var item = Item("PLT-TASK-1");
        var context = Context(item);
        _ = DryRunEngine.Run(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Другое"]), context);

        Assert.Equal("Старое", context.Items["PLT-TASK-1"].Values["title"]);
    }

    [Fact]
    public void MissingColumns_NamesTheOnesTheFileLacks()
    {
        var settings = Settings(Col("description", "Описание"));
        var headers = DryRunEngine.HeaderIndex(Table(["заголовок"]).Headers);

        Assert.Equal(["Описание"], DryRunEngine.MissingColumns(settings, headers));
    }

    [Fact]
    public void Totals_CountByResult()
    {
        var rows = DryRunEngine.Run(Settings(), Table(["Заголовок"], ["А"], [""], ["Б"]), Context()).Rows;
        var totals = DryRunTotals.Of(rows);

        Assert.Equal((2, 1, 3), (totals.Created, totals.Skipped, totals.All));
    }
}
