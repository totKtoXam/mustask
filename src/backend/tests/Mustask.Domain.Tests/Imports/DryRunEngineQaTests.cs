using System.Text.Json;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Source;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Process;
using Xunit;

namespace Mustask.Domain.Tests.Imports;

/// <summary>QA additions for the dry run (mod-024, backend QA): the corners the developer's tests leave open. Made-up data only.</summary>
public sealed class DryRunEngineQaTests
{
    private static readonly Guid PlatformId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TaskTypeId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid BugTypeId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid TodoId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid DoneId = Guid.Parse("00000000-0000-0000-0000-0000000000c2");
    private static readonly Guid FixedId = Guid.Parse("00000000-0000-0000-0000-0000000000c4");
    private static readonly Guid TeamId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    private static DryRunContext Context(bool teamLinked = false, Func<Guid, bool>? canRead = null, params DryRunItemRef[] items) => new()
    {
        Projects = new Dictionary<Guid, DryRunProjectRef> { [PlatformId] = new(PlatformId, "Платформа", true) },
        Types = new Dictionary<Guid, DryRunTypeRef>
        {
            [TaskTypeId] = new(TaskTypeId, "Задача", AppearanceToken.Task, [new(TodoId, "К выполнению", false), new(DoneId, "Готово", false)]),
            [BugTypeId] = new(BugTypeId, "Баг", AppearanceToken.Bug, [new(TodoId, "К выполнению", false), new(FixedId, "Исправлено", false)]),
        },
        Members = new Dictionary<string, DryRunMemberRef> { ["user-1"] = new("user-1", "Анна Тестова", true) },
        Teams = new Dictionary<Guid, string> { [TeamId] = "Команда 1" },
        ProjectTeams = teamLinked
            ? new Dictionary<Guid, IReadOnlySet<Guid>> { [PlatformId] = new HashSet<Guid> { TeamId } }
            : new Dictionary<Guid, IReadOnlySet<Guid>>(),
        Items = items.ToDictionary(i => DryRunEngine.NormalizeIdentifier(i.Identifier)),
        CanImportInto = _ => true,
        CanRead = canRead ?? (_ => true),
    };

    private static DryRunItemRef Item(string identifier, Guid? type = null, string title = "Старое", string? description = null) =>
        new(Guid.NewGuid(), identifier, PlatformId, type ?? TaskTypeId, false,
            new Dictionary<string, string?> { ["title"] = title, ["description"] = description, ["state"] = TodoId.ToString(), ["priority"] = "2" });

    private static SourceTable Table(string[] headers, params string[][] rows) => new(
        SourceFormat.Csv, ["s"], null, 1, 2, "utf-8", ";", false, false, null,
        headers.Select((h, i) => new SourceHeader(i, h, CellAddress.Of(i, 1))).ToList(),
        rows.Select((r, n) => new SourceRow(n + 2, r.Select((c, i) => SourceCell.OfText(c, CellAddress.Of(i, n + 2))).ToList())).ToList(),
        rows.Length);

    private static FieldMapping Col(string field, string column) =>
        new() { Id = Guid.NewGuid(), Field = field, Source = new MappingSource { Kind = TemplateTokens.SourceColumn, Column = column } };

    private static FieldMapping Const(string field, string value) =>
        new() { Id = Guid.NewGuid(), Field = field, Source = new MappingSource { Kind = TemplateTokens.SourceConstant, Value = value } };

    private static TemplateSettings Settings(params FieldMapping[] extra) => new()
    {
        Mappings = [Const("project", PlatformId.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок"), .. extra],
    };

    private static RecodingTableSettings Table(FieldMapping mapping, string policy, params RecodingEntrySettings[] entries) =>
        new() { MappingId = mapping.Id, Policy = policy, Entries = entries };

    private static DryRunRowResult Single(TemplateSettings settings, SourceTable table, DryRunContext context) => Assert.Single(DryRunEngine.Run(settings, table, context).Rows);

    private static bool Has(DryRunRowResult row, string code) => row.Reasons.Any(r => r.Code == code);

    [Fact(Skip = "QA D-2 (open): the diff compares ignoring case, so a case-only change reads as unchanged; remove Skip when fixed")]
    public void Update_ThatOnlyChangesLetterCase_IsAnUpdate()
    {
        var row = Single(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "СТАРОЕ"]), Context(items: Item("PLT-TASK-1")));

        Assert.Equal(RowResults.Updated, row.Result);
    }

    [Fact(Skip = "QA D-2 (open): same defect on the description")]
    public void Update_ThatOnlyChangesTheCaseOfTheDescription_IsAnUpdate()
    {
        var row = Single(
            Settings(Col("description", "Описание")),
            Table(["Идентификатор", "Заголовок", "Описание"], ["PLT-TASK-1", "Старое", "Текст"]),
            Context(items: Item("PLT-TASK-1", description: "текст")));

        Assert.Equal(RowResults.Updated, row.Result);
    }

    [Fact]
    public void OwningTeam_NotLinkedToTheProject_IsAnError_AndLinkedIsAccepted()
    {
        var team = Col("owningTeam", "Команда");
        var settings = Settings(team) with
        {
            Recoding = [Table(team, TemplateTokens.PolicyError, new RecodingEntrySettings { FileValue = "К1", Target = TeamId.ToString() })],
        };
        var file = Table(["Заголовок", "Команда"], ["А", "к1"]);

        Assert.True(Has(Single(settings, file, Context(teamLinked: false)), ImportErrorCodes.RowTeamNotLinked));
        Assert.Equal(RowResults.Created, Single(settings, file, Context(teamLinked: true)).Result);
    }

    [Fact]
    public void State_TypeSpecificEntryWinsOnThatType_AndTypelessEntryServesTheOthers()
    {
        var type = Col("type", "Вид");
        var state = Col("state", "Статус");
        var settings = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), type, Col("title", "Заголовок"), state],
            Recoding =
            [
                Table(type, TemplateTokens.PolicyError,
                    new RecodingEntrySettings { FileValue = "Задача", Target = TaskTypeId.ToString() },
                    new RecodingEntrySettings { FileValue = "Баг", Target = BugTypeId.ToString() }),
                Table(state, TemplateTokens.PolicyError,
                    new RecodingEntrySettings { FileValue = "Проверено", Target = TodoId.ToString() },
                    new RecodingEntrySettings { FileValue = "проверено", TypeId = BugTypeId, Target = FixedId.ToString() }),
            ],
        };
        var rows = DryRunEngine.Run(settings, Table(["Вид", "Заголовок", "Статус"], ["Баг", "А", " ПРОВЕРЕНО "], ["Задача", "Б", "Проверено"]), Context()).Rows;

        Assert.Equal(RowResults.Created, rows[0].Result);
        Assert.Contains(rows[0].Fields, f => f.Field == "state" && f.Value == "Исправлено");
        Assert.Contains(rows[1].Fields, f => f.Field == "state" && f.Value == "К выполнению");
    }

    [Fact]
    public void State_TargetOfAnotherType_IsStateNotFound()
    {
        var type = Col("type", "Вид");
        var state = Col("state", "Статус");
        var settings = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), type, Col("title", "Заголовок"), state],
            Recoding =
            [
                Table(type, TemplateTokens.PolicyError, new RecodingEntrySettings { FileValue = "Задача", Target = TaskTypeId.ToString() }),
                Table(state, TemplateTokens.PolicyError, new RecodingEntrySettings { FileValue = "Исправлено", Target = FixedId.ToString() }),
            ],
        };

        var row = Single(settings, Table(["Вид", "Заголовок", "Статус"], ["Задача", "А", "Исправлено"]), Context());

        Assert.Equal(RowResults.Error, row.Result);
        Assert.True(Has(row, ImportErrorCodes.RowStateNotFound));
    }

    [Fact]
    public void RowWithSeveralProblems_ListsEveryReason()
    {
        var type = Col("type", "Вид");
        var date = Col("targetDate", "Срок");
        var settings = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), type, Col("title", "Заголовок"), date],
            Recoding = [Table(type, TemplateTokens.PolicyError, new RecodingEntrySettings { FileValue = "Задача", Target = TaskTypeId.ToString() })],
        };

        var row = Single(settings, Table(["Вид", "Заголовок", "Срок"], ["Неизвестный", "А", "31.02.2026"]), Context());

        Assert.Equal(RowResults.Error, row.Result);
        Assert.True(row.Reasons.Count(r => r.Severity == "error") >= 2, "the unknown type and the unreadable date are both reasons");
    }

    [Fact]
    public void SkipSetting_WithoutAnIdentifierColumn_StillCreates()
    {
        var settings = Settings() with { Identifier = new IdentifierSettings { EmptyIdentifier = TemplateTokens.IdentifierSkip } };

        Assert.Equal(RowResults.Created, Single(settings, Table(["Заголовок"], ["А"]), Context()).Result);
    }

    [Fact]
    public void Identifier_OfAnItemInAProjectTheImporterCannotRead_IsNotFound_AndNeverCreates()
    {
        var row = Single(Settings(), Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Новое"]), Context(canRead: _ => false, items: Item("PLT-TASK-1")));

        Assert.Equal(RowResults.Error, row.Result);
        Assert.True(Has(row, ImportErrorCodes.RowIdentifierNotFound));
        Assert.False(Has(row, ImportErrorCodes.RowIdentifierDeleted));
    }

    [Fact]
    public void Identifier_CompareIgnoresCaseAndSpaces_AndTwoSpellingsAreDuplicates()
    {
        var rows = DryRunEngine.Run(
            Settings(), Table(["Идентификатор", "Заголовок"], ["plt-task-1", "А"], [" PLT-TASK-1 ", "Б"]), Context(items: Item("PLT-TASK-1"))).Rows;

        Assert.All(rows, r => Assert.Equal(RowResults.Error, r.Result));
        Assert.Contains(rows[0].Reasons, r => r.Code == ImportErrorCodes.RowIdentifierDuplicate && r.Params["row"] == "3");
        Assert.Contains(rows[1].Reasons, r => r.Code == ImportErrorCodes.RowIdentifierDuplicate && r.Params["row"] == "2");
    }

    [Fact]
    public void Identifier_UpdateWithAProjectConstantOfAnotherProject_IsProjectChanged()
    {
        var other = Guid.Parse("00000000-0000-0000-0000-0000000000a9");
        var context = Context(items: Item("PLT-TASK-1"));
        var withOther = new DryRunContext
        {
            Projects = new Dictionary<Guid, DryRunProjectRef>(context.Projects) { [other] = new(other, "Финансы", true) },
            Types = context.Types, Members = context.Members, Teams = context.Teams, ProjectTeams = context.ProjectTeams, Items = context.Items,
            CanImportInto = context.CanImportInto, CanRead = context.CanRead,
        };
        var settings = new TemplateSettings { Mappings = [Const("project", other.ToString()), Const("type", TaskTypeId.ToString()), Col("title", "Заголовок")] };

        var row = Single(settings, Table(["Идентификатор", "Заголовок"], ["PLT-TASK-1", "Старое"]), withOther);

        Assert.True(Has(row, ImportErrorCodes.RowProjectChanged));
    }

    [Fact]
    public void MissingColumns_AlsoNamesAMergePlaceholderTheFileLacks_IgnoringCase()
    {
        var merge = new FieldMapping
        {
            Id = Guid.NewGuid(), Field = "description", Source = new MappingSource { Kind = TemplateTokens.SourceMerge, Pattern = "{Система} / {Модуль}" },
        };
        var headers = DryRunEngine.HeaderIndex(Table([" система ", "Заголовок"]).Headers);

        Assert.Equal(["Модуль"], DryRunEngine.MissingColumns(Settings(merge), headers));
    }

    private static FieldMapping WithChain(FieldMapping mapping, string converter, string json = "{}") => mapping with
    {
        Chain = [new ChainStepSettings { Converter = converter, Params = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) }],
    };

    [Fact(Skip = "QA D-4 (open): the template's extract step carries no group, so the whole match is written instead of the first group; remove Skip when fixed")]
    public void Extract_OnATemplateStep_TakesTheFirstGroup_NotTheWholeMatch()
    {
        var settings = new TemplateSettings
        {
            Mappings = [Const("project", PlatformId.ToString()), Const("type", TaskTypeId.ToString()), WithChain(Col("title", "Задача"), "extract", "{\"pattern\":\"#(\\\\d+)\"}")],
        };

        var row = Single(settings, Table(["Задача"], ["Задача #4521"]), Context());

        Assert.Equal("4521", row.Title);
    }

    [Fact(Skip = "QA D-3 (open): «Дата из ISO 8601» refuses a date-time (with or without an offset); remove Skip when fixed")]
    public void IsoDate_OnATemplateStep_ReadsADateTimeWithOffsetAsWritten()
    {
        var table = Table(["Начало"], ["2026-03-15"], ["2026-03-15T23:30:00-05:00"], ["2026-03-15T23:30:00"]);
        var headers = DryRunEngine.HeaderIndex(table.Headers);
        var mapping = WithChain(Col("startDate", "Начало"), "isoDate");

        foreach (var row in table.Rows)
        {
            var evaluation = DryRunEngine.Evaluate(mapping, row, headers, null);

            Assert.Null(evaluation.Failure);
            Assert.Equal(new DateOnly(2026, 3, 15), evaluation.Value.Date);
        }
    }

    [Fact]
    public void Volume_5000RowsBy50Columns_RunsFarInsideTheSixtySecondBudget()
    {
        var headers = Enumerable.Range(0, 50).Select(i => $"К{i}").Append("Заголовок").Append("Идентификатор").ToArray();
        var rows = Enumerable.Range(0, 5000)
            .Select(n => headers.Select(h => h == "Заголовок" ? $"Задача {n}" : h == "Идентификатор" ? $"PLT-TASK-{n}" : $" значение {n} ").ToArray())
            .ToArray();
        var extra = Enumerable.Range(0, 10).Select(i => WithChain(Col("description", $"К{i}"), "trim")).Take(1).ToArray();
        var settings = Settings(extra);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var outcome = DryRunEngine.Run(settings, Table(headers, rows), Context());

        Assert.Equal(5000, outcome.Rows.Count);
        Assert.All(outcome.Rows, r => Assert.Equal(RowResults.Error, r.Result)); // the identifiers name no item
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }

    [Fact]
    public void Priority_OutOfRange_IsValueInvalid_AndEveryFileRowGetsExactlyOneResult()
    {
        var rows = DryRunEngine.Run(
            Settings(Col("priority", "Важность")),
            Table(["Заголовок", "Важность"], ["А", "7"], ["Б", "3"], ["", ""], ["В", "abc"]), Context()).Rows;

        Assert.Equal(4, rows.Count);
        Assert.Equal([RowResults.Error, RowResults.Created, RowResults.Skipped, RowResults.Error], rows.Select(r => r.Result));
        Assert.Equal(4, DryRunTotals.Of(rows).All);
    }
}
