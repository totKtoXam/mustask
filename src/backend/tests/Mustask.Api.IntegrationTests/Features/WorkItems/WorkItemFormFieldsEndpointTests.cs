using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-022's fields per type on mod-003's routes (feat-022 api-contract § 1–§ 3):
/// the create answers the defaults (US-5), the read carries the 17 fields on
/// every type, and the form's save sets, empties or leaves each, refusing a
/// field the type lacks and a value out of its range (US-1..US-5). Every
/// scenario is named after its story.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemFormFieldsEndpointTests(MustaskApiFactory factory)
{
    /// <summary>feat-022 api-contract § 1, in its order.</summary>
    private static readonly string[] FieldNames =
    [
        "priority", "risk", "storyPoints", "effort", "businessValue", "timeCriticality", "startDate", "targetDate", "severity",
        "activity", "originalEstimate", "remainingWork", "completedWork", "valueArea", "acceptanceCriteria", "reproSteps", "systemInfo",
    ];

    private async Task<(WorkItemsWorld World, string Identifier)> ItemAsync(string typeKey, string title = "Элемент")
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        return (world, await WorkItemsTestData.CreateItemAsync(factory, world, typeKey, title));
    }

    /// <summary>The JSON text of each named field, «name=value» joined by «; ».</summary>
    private static string Raw(JsonElement item, params string[] names) =>
        string.Join("; ", names.Select(name => $"{name}={item.GetProperty(name).GetRawText()}"));

    private async Task<JsonElement> SavedAsync(WorkItemsWorld world, string identifier, object body)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world, identifier, body);

        Assert.True(answer.Status == HttpStatusCode.OK, $"The save answered {answer.Status} {answer.ErrorCode} {answer.Body}.");

        return answer.Body;
    }

    /// <summary>A 400 with exactly these field reasons, each with the catalog's Russian text.</summary>
    private static void AssertReasons(Answer answer, params (string Field, string Code, string Text)[] reasons)
    {
        Assert.True(answer.Status == HttpStatusCode.BadRequest, $"Expected 400, got {answer.Status} {answer.ErrorCode}.");
        Assert.Equal("workitems.validation_failed", answer.ErrorCode);

        var errors = answer.Body.GetProperty("errors").EnumerateObject()
            .Select(field =>
            {
                var reason = Assert.Single(field.Value.EnumerateArray());

                return (field.Name, reason.GetProperty("code").GetString()!, reason.GetProperty("detail").GetString()!);
            })
            .OrderBy(error => error.Name, StringComparer.Ordinal);

        Assert.Equal(reasons.OrderBy(reason => reason.Field, StringComparer.Ordinal), errors);
    }

    private const string OutOfRange = "workitems.workitem.number_out_of_range";
    private const string OutOfRangeText = "Допустимо значение от 0 до 9 999.";
    private const string TooPrecise = "workitems.workitem.number_too_precise";
    private const string TooPreciseText = "Не более двух знаков после запятой.";
    private const string NotInList = "workitems.workitem.value_not_in_list";
    private const string NotInListText = "Выберите значение из списка.";
    private const string NotOnType = "workitems.workitem.field_not_on_type";
    private const string NotOnTypeText = "Это поле недоступно для рабочего элемента этого типа.";
    private const string TextTooLong = "workitems.workitem.text_too_long";
    private const string TextTooLongText = "Не более 100 000 символов.";

    // --- US-5 ------------------------------------------------------------------------------------------------------

    // US-5 · "A new item takes its type's defaults" (BR-M3-37, BR-M3-40): the create answers them, the read too; every
    // other field of this task is null, the type's texts "" (US-4 "The field starts empty"), a field the type lacks null.
    [Theory]
    [InlineData("EPIC", "priority=2; risk=null; storyPoints=null; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null; severity=null; activity=null; originalEstimate=null; remainingWork=null; completedWork=null; valueArea=\"Business\"; acceptanceCriteria=null; reproSteps=null; systemInfo=null")]
    [InlineData("FEAT", "priority=2; risk=null; storyPoints=null; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null; severity=null; activity=null; originalEstimate=null; remainingWork=null; completedWork=null; valueArea=\"Business\"; acceptanceCriteria=null; reproSteps=null; systemInfo=null")]
    [InlineData("US", "priority=2; risk=null; storyPoints=null; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null; severity=null; activity=null; originalEstimate=null; remainingWork=null; completedWork=null; valueArea=\"Business\"; acceptanceCriteria=\"\"; reproSteps=null; systemInfo=null")]
    [InlineData("TASK", "priority=2; risk=null; storyPoints=null; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null; severity=null; activity=null; originalEstimate=null; remainingWork=null; completedWork=null; valueArea=null; acceptanceCriteria=null; reproSteps=null; systemInfo=null")]
    [InlineData("BUG", "priority=2; risk=null; storyPoints=null; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null; severity=3; activity=null; originalEstimate=null; remainingWork=null; completedWork=null; valueArea=null; acceptanceCriteria=null; reproSteps=\"\"; systemInfo=\"\"")]
    public async Task Create_EachType_AnswersItsDefaults_AndTheReadKeepsThem(string typeKey, string expected)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var created = await WorkItemsTestData.CreateAsync(factory, world, typeKey, "С одним заголовком");

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(expected, Raw(created.Body, FieldNames));
        Assert.Equal(expected, Raw(await WorkItemsTestData.ReadAsync(factory, world, created.Body.GetProperty("identifier").GetString()!), FieldNames));
    }

    // US-5 · "The list and the create dialog do not change" (BR-M3-40): the list's row carries no field of this task, and
    // a create body that names one is read as mod-003's create reads any member it does not know — ignored.
    [Fact]
    public async Task ListRow_CarriesNoNewField_CreateIgnoresThem()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var created = await OrganizationsApiSendCreateAsync(world, new
        {
            requestId = Guid.NewGuid().ToString(),
            typeId = world.Types["TASK"].Id.ToString(),
            title = "Задача",
            priority = new { value = 1 },
            remainingWork = new { value = 5 },
        });
        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("priority=2; remainingWork=null", Raw(created.Body, "priority", "remainingWork"));
        Assert.All(FieldNames, name => Assert.False(row.TryGetProperty(name, out _), $"The list's row carries {name}."));
    }

    private Task<Answer> OrganizationsApiSendCreateAsync(WorkItemsWorld world, object body) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, WorkItemsTestData.List(world), body, OrganizationsTokens.SignedIn());

    // US-5 · "Every type has Приоритет 1..4": 1 is saved and kept on each of the five types.
    [Theory]
    [InlineData("EPIC")]
    [InlineData("FEAT")]
    [InlineData("US")]
    [InlineData("TASK")]
    [InlineData("BUG")]
    public async Task Save_Priority_IsKeptOnEveryType(string typeKey)
    {
        var (world, item) = await ItemAsync(typeKey);

        var saved = await SavedAsync(world, item, new { priority = new { value = 1 } });

        Assert.Equal(1, saved.GetProperty("priority").GetInt32());
        Assert.Equal(1, (await WorkItemsTestData.ReadAsync(factory, world, item)).GetProperty("priority").GetInt32());
    }

    // US-5 · "Приоритет cannot be emptied" (BR-M3-31): a task of priority 4 emptied — `{ value: null }` or `{}` — reads 2.
    [Fact]
    public async Task Save_PriorityEmptied_StoresTwo()
    {
        var (world, task) = await ItemAsync("TASK");
        await SavedAsync(world, task, new { priority = new { value = 4 } });

        var emptied = await SavedAsync(world, task, new { priority = new { value = (int?)null } });
        await SavedAsync(world, task, new { priority = new { value = 3 } });
        var emptiedByBareObject = await SavedAsync(world, task, new { priority = new { } });

        Assert.Equal(2, emptied.GetProperty("priority").GetInt32());
        Assert.Equal(2, emptiedByBareObject.GetProperty("priority").GetInt32());
    }

    // US-5 · "A priority outside 1..4 is refused" (BR-M3-31): the item unchanged.
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Save_PriorityOutsideOneToFour_IsValueNotInList_Unchanged(int priority)
    {
        var (world, item) = await ItemAsync("US");
        var before = (await WorkItemsTestData.ReadAsync(factory, world, item)).GetRawText();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, item, new { priority = new { value = priority }, title = "Иное" });

        AssertReasons(answer, ("priority", NotInList, NotInListText));
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, item)).GetRawText());
    }

    // US-5 · "A field of another type is refused", US-4 · "The field cannot be written onto another type" (BR-M3-30): even
    // an emptying value is refused, and the item is unchanged.
    [Theory]
    [InlineData("TASK", "storyPoints")]
    [InlineData("US", "originalEstimate")]
    [InlineData("BUG", "valueArea")]
    [InlineData("EPIC", "severity")]
    [InlineData("TASK", "startDate")]
    [InlineData("TASK", "acceptanceCriteria")]
    [InlineData("US", "reproSteps")]
    [InlineData("EPIC", "systemInfo")]
    public async Task Save_AFieldTheTypeLacks_IsFieldNotOnType_Unchanged(string typeKey, string field)
    {
        var (world, item) = await ItemAsync(typeKey);
        var before = (await WorkItemsTestData.ReadAsync(factory, world, item)).GetRawText();
        var body = new Dictionary<string, object?>
        {
            ["title"] = "Иное",
            [field] = field is "acceptanceCriteria" or "reproSteps" or "systemInfo" ? "Текст" : new { value = (object?)null },
        };

        var answer = await WorkItemsTestData.SaveAsync(factory, world, item, body);

        AssertReasons(answer, (field, NotOnType, NotOnTypeText));
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, item)).GetRawText());
    }

    // --- US-2 ------------------------------------------------------------------------------------------------------

    // US-2 · "An epic's planning and classification are saved", "a «Фича» offers and keeps the same fields the same way";
    // a number is answered in its shortest form (api-contract § 1).
    [Theory]
    [InlineData("EPIC")]
    [InlineData("FEAT")]
    public async Task Save_PortfolioPlanningAndClassification_AreKept(string typeKey)
    {
        var (world, item) = await ItemAsync(typeKey);

        var saved = await SavedAsync(world, item, new
        {
            risk = new { value = 1 },
            effort = new { value = 13.50m },
            businessValue = new { value = 40 },
            timeCriticality = new { value = 8 },
            startDate = new { value = "2026-11-01" },
            targetDate = new { value = "2027-03-31" },
            valueArea = new { value = "Architectural" },
        });
        var reopened = await WorkItemsTestData.ReadAsync(factory, world, item);

        const string Expected =
            "risk=1; effort=13.5; businessValue=40; timeCriticality=8; startDate=\"2026-11-01\"; targetDate=\"2027-03-31\"; valueArea=\"Architectural\"";
        string[] names = ["risk", "effort", "businessValue", "timeCriticality", "startDate", "targetDate", "valueArea"];
        Assert.Equal(Expected, Raw(saved, names));
        Assert.Equal(Expected, Raw(reopened, names));
    }

    // US-2 · "A story's planning and classification are saved", and the story carries none of the portfolio's fields.
    [Fact]
    public async Task Save_StoryPlanningAndClassification_AreKept()
    {
        var (world, story) = await ItemAsync("US");

        await SavedAsync(world, story, new { storyPoints = new { value = 3 }, risk = new { value = 3 }, valueArea = new { value = "Business" } });
        var reopened = await WorkItemsTestData.ReadAsync(factory, world, story);

        Assert.Equal(
            "storyPoints=3; risk=3; valueArea=\"Business\"; effort=null; businessValue=null; timeCriticality=null; startDate=null; targetDate=null",
            Raw(reopened, "storyPoints", "risk", "valueArea", "effort", "businessValue", "timeCriticality", "startDate", "targetDate"));
    }

    // US-2 · "Risk can be emptied, the value area cannot" (BR-M3-31).
    [Fact]
    public async Task Save_RiskEmptied_IsNull_ValueAreaEmptied_IsBusiness_AnotherKeyRefused()
    {
        var (world, feature) = await ItemAsync("FEAT");
        await SavedAsync(world, feature, new { risk = new { value = 2 }, valueArea = new { value = "Architectural" } });

        var emptied = await SavedAsync(world, feature, new { risk = new { value = (int?)null }, valueArea = new { value = (string?)null } });
        var refused = await WorkItemsTestData.SaveAsync(factory, world, feature, new { valueArea = new { value = "Technical" } });

        Assert.Equal("risk=null; valueArea=\"Business\"", Raw(emptied, "risk", "valueArea"));
        AssertReasons(refused, ("valueArea", NotInList, NotInListText));
    }

    // US-2 · "A number field may be emptied, and 0 is kept as 0" (BR-M3-32).
    [Fact]
    public async Task Save_NumberEmptied_IsNull_ZeroIsKept()
    {
        var (world, epic) = await ItemAsync("EPIC");
        await SavedAsync(world, epic, new { effort = new { value = 5 } });

        var emptied = await SavedAsync(world, epic, new { effort = new { value = (decimal?)null } });
        var zero = await SavedAsync(world, epic, new { effort = new { value = 0 } });

        Assert.Equal("effort=null", Raw(emptied, "effort"));
        Assert.Equal("effort=0", Raw(zero, "effort"));
    }

    // US-2 · "An invalid number is refused, not rounded" (BR-M3-32): the field's text, the item unchanged.
    [Theory]
    [InlineData("effort", "-1", OutOfRange, OutOfRangeText)]
    [InlineData("timeCriticality", "10000", OutOfRange, OutOfRangeText)]
    [InlineData("effort", "2.555", TooPrecise, TooPreciseText)]
    [InlineData("businessValue", "2.5", "workitems.workitem.integer_required", "Введите целое число.")]
    public async Task Save_InvalidNumber_IsRefused_Unchanged(string field, string value, string code, string text)
    {
        var (world, epic) = await ItemAsync("EPIC");
        await SavedAsync(world, epic, new { effort = new { value = 5 }, businessValue = new { value = 7 }, timeCriticality = new { value = 3 } });
        var before = (await WorkItemsTestData.ReadAsync(factory, world, epic)).GetRawText();

        var answer = await WorkItemsTestData.SaveAsync(
            factory,
            world,
            epic,
            new Dictionary<string, object> { [field] = new { value = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture) } });

        AssertReasons(answer, (field, code, text));
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, epic)).GetRawText());
    }

    // US-2 · "The target date may not precede the start date" (BR-M3-35, OQ-3 (a)): reported on «Целевая дата» when only
    // the start was sent; both in one save in order are accepted; either alone may be set or cleared.
    [Fact]
    public async Task Save_TargetBeforeStart_IsRefusedOnTheTarget_InOrderAccepted()
    {
        var (world, epic) = await ItemAsync("EPIC");
        await SavedAsync(world, epic, new { targetDate = new { value = "2027-03-31" } });
        var before = (await WorkItemsTestData.ReadAsync(factory, world, epic)).GetRawText();

        var refused = await WorkItemsTestData.SaveAsync(factory, world, epic, new { startDate = new { value = "2027-04-01" } });
        AssertReasons(refused, ("targetDate", "workitems.workitem.target_before_start", "Целевая дата не может быть раньше даты начала."));
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, epic)).GetRawText());

        var together = await SavedAsync(world, epic, new { startDate = new { value = "2027-04-01" }, targetDate = new { value = "2027-06-30" } });
        Assert.Equal("startDate=\"2027-04-01\"; targetDate=\"2027-06-30\"", Raw(together, "startDate", "targetDate"));

        var targetCleared = await SavedAsync(world, epic, new { targetDate = new { value = (string?)null } });
        var startCleared = await SavedAsync(world, epic, new { startDate = new { } });
        var targetAlone = await SavedAsync(world, epic, new { targetDate = new { value = "2026-01-15" } });

        Assert.Equal("startDate=\"2027-04-01\"; targetDate=null", Raw(targetCleared, "startDate", "targetDate"));
        Assert.Equal("startDate=null; targetDate=null", Raw(startCleared, "startDate", "targetDate"));
        Assert.Equal("startDate=null; targetDate=\"2026-01-15\"", Raw(targetAlone, "startDate", "targetDate"));
    }

    // US-2 · "A date outside 2000–2099 is refused" (BR-M3-35).
    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("2100-01-01")]
    public async Task Save_DateOutside2000To2099_IsDateOutOfRange(string date)
    {
        var (world, epic) = await ItemAsync("EPIC");

        var answer = await WorkItemsTestData.SaveAsync(factory, world, epic, new { startDate = new { value = date } });

        AssertReasons(answer, ("startDate", "workitems.workitem.date_out_of_range", "Укажите дату с 2000 по 2099 год."));
    }

    // api-contract § 3.2: a value of the wrong JSON type fails binding with the platform's 400 before any check.
    [Theory]
    [InlineData("{\"startDate\":{\"value\":\"31.12.2027\"}}")]
    [InlineData("{\"startDate\":{\"value\":\"2027-02-30\"}}")]
    [InlineData("{\"priority\":{\"value\":2.5}}")]
    [InlineData("{\"effort\":{\"value\":\"много\"}}")]
    [InlineData("{\"risk\":2}")]
    [InlineData("{\"acceptanceCriteria\":{\"value\":\"текст\"}}")]
    public async Task Save_WrongJsonType_IsThePlatformsBadRequest(string json)
    {
        var (world, epic) = await ItemAsync("EPIC");

        using var request = new HttpRequestMessage(HttpMethod.Patch, WorkItemsTestData.Item(world, epic))
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.WithBearer(OrganizationsTokens.SignedIn());
        using var response = await factory.CreateClient().SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("http.bad_request", body.RootElement.GetProperty("errorCode").GetString());
    }

    // --- US-3 ------------------------------------------------------------------------------------------------------

    // US-3 · "A task's estimates are saved in hours": «5,5» and «2.5» come back as 5.5 and 2.5; the task carries no
    // «Оценка в пунктах» and no «Серьёзность».
    [Fact]
    public async Task Save_TaskEstimates_AreKept()
    {
        var (world, task) = await ItemAsync("TASK");

        await SavedAsync(world, task, new
        {
            activity = new { value = "Development" },
            originalEstimate = new { value = 8 },
            remainingWork = new { value = 5.5m },
            completedWork = new { value = 2.50m },
        });
        var reopened = await WorkItemsTestData.ReadAsync(factory, world, task);

        Assert.Equal(
            "activity=\"Development\"; originalEstimate=8; remainingWork=5.5; completedWork=2.5; storyPoints=null; severity=null",
            Raw(reopened, "activity", "originalEstimate", "remainingWork", "completedWork", "storyPoints", "severity"));
    }

    // US-3 · "A bug carries points, severity, activity and hours".
    [Fact]
    public async Task Save_BugPointsSeverityAndHours_AreKept()
    {
        var (world, bug) = await ItemAsync("BUG");

        await SavedAsync(world, bug, new { storyPoints = new { value = 1 }, severity = new { value = 1 }, remainingWork = new { value = 3 } });

        Assert.Equal(
            "storyPoints=1; severity=1; remainingWork=3",
            Raw(await WorkItemsTestData.ReadAsync(factory, world, bug), "storyPoints", "severity", "remainingWork"));
    }

    // US-3 · "Hours are refused beyond two decimals, never rounded": the stored value unchanged.
    [Fact]
    public async Task Save_HoursWithThreeDecimals_AreRefused_StoredValueKept()
    {
        var (world, task) = await ItemAsync("TASK");
        await SavedAsync(world, task, new { originalEstimate = new { value = 4 } });

        var answer = await WorkItemsTestData.SaveAsync(factory, world, task, new { originalEstimate = new { value = 1.333m } });

        AssertReasons(answer, ("originalEstimate", TooPrecise, TooPreciseText));
        Assert.Equal("originalEstimate=4", Raw(await WorkItemsTestData.ReadAsync(factory, world, task), "originalEstimate"));
    }

    // US-3 · "An empty estimate is not zero" (BR-M3-32).
    [Fact]
    public async Task Save_EstimateZeroThenCleared_IsZeroThenEmpty()
    {
        var (world, task) = await ItemAsync("TASK");

        var zero = await SavedAsync(world, task, new { remainingWork = new { value = 0 } });
        var cleared = await SavedAsync(world, task, new { remainingWork = new { value = (decimal?)null } });

        Assert.Equal(("remainingWork=0", "remainingWork=null"), (Raw(zero, "remainingWork"), Raw(cleared, "remainingWork")));
    }

    // US-3 · "Closing a task leaves its remaining work as entered" and "No field changes another" (BR-M3-34).
    [Fact]
    public async Task Save_StateChangeAndOtherEstimates_LeaveRemainingWorkAsEntered()
    {
        var (world, task) = await ItemAsync("TASK");
        await SavedAsync(world, task, new { originalEstimate = new { value = 8 }, remainingWork = new { value = 5 }, completedWork = new { value = 3 } });

        await SavedAsync(world, task, new { completedWork = new { value = 6 } });
        var closed = await SavedAsync(world, task, new { stateId = world.Types["TASK"].StateId("Закрыта").ToString() });

        Assert.Equal("originalEstimate=8; remainingWork=5; completedWork=6", Raw(closed, "originalEstimate", "remainingWork", "completedWork"));
    }

    // US-3 · "Severity cannot be emptied" (BR-M3-31).
    [Fact]
    public async Task Save_SeverityEmptied_IsThree()
    {
        var (world, bug) = await ItemAsync("BUG");
        await SavedAsync(world, bug, new { severity = new { value = 1 } });

        var emptied = await SavedAsync(world, bug, new { severity = new { value = (int?)null } });

        Assert.Equal(3, emptied.GetProperty("severity").GetInt32());
    }

    // US-3 · "A value outside the list is refused" (BR-M3-31): an unknown key, a key in another letter case, a number.
    [Theory]
    [InlineData("Coding")]
    [InlineData("development")]
    [InlineData("2")]
    public async Task Save_ActivityOutsideTheList_IsValueNotInList_Unchanged(string key)
    {
        var (world, task) = await ItemAsync("TASK");
        var before = (await WorkItemsTestData.ReadAsync(factory, world, task)).GetRawText();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, task, new { activity = new { value = key } });

        AssertReasons(answer, ("activity", NotInList, NotInListText));
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, task)).GetRawText());
    }

    // --- US-4 ------------------------------------------------------------------------------------------------------

    // US-4 · "The field starts empty and is saved with the other fields": one save stores both texts and the priority, and
    // the list's «Изменён» is that save's time.
    [Fact]
    public async Task Save_BugTextsWithPriority_InOneSave_ListShowsThatSave()
    {
        var (world, bug) = await ItemAsync("BUG");

        var saved = await SavedAsync(world, bug, new { reproSteps = "1. Открыть\n2. Войти", systemInfo = "Windows 11, Edge 140", priority = new { value = 1 } });
        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];

        Assert.Equal(
            "reproSteps=\"1. Открыть\\n2. Войти\"; systemInfo=\"Windows 11, Edge 140\"; priority=1",
            Raw(await WorkItemsTestData.ReadAsync(factory, world, bug), "reproSteps", "systemInfo", "priority"));
        Assert.Equal(saved.GetProperty("changedAt").GetDateTimeOffset(), row.GetProperty("changedAt").GetDateTimeOffset());
    }

    // US-4 · "The text is plain" (BR-M3-36): markup and the line break kept as entered.
    [Fact]
    public async Task Save_AcceptanceCriteria_IsKeptAsEntered()
    {
        var (world, story) = await ItemAsync("US");

        var saved = await SavedAsync(world, story, new { acceptanceCriteria = "<b>важно</b>\nшаг 2" });

        Assert.Equal("<b>важно</b>\nшаг 2", saved.GetProperty("acceptanceCriteria").GetString());
    }

    // US-4 · "The length limit holds on the server" (BR-M3-36): 100 001 characters refused on either bug text, the item
    // unchanged; 100 000 are accepted.
    [Fact]
    public async Task Save_TextOver100000_IsTextTooLong_100000Accepted()
    {
        var (world, bug) = await ItemAsync("BUG");
        var before = (await WorkItemsTestData.ReadAsync(factory, world, bug)).GetRawText();

        var refused = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { reproSteps = new string('ж', 100_001), systemInfo = new string('ж', 100_001) });
        Assert.Equal(before, (await WorkItemsTestData.ReadAsync(factory, world, bug)).GetRawText());
        var longest = await SavedAsync(world, bug, new { reproSteps = new string('ж', 100_000) });

        AssertReasons(refused, ("reproSteps", TextTooLong, TextTooLongText), ("systemInfo", TextTooLong, TextTooLongText));
        Assert.Equal(100_000, longest.GetProperty("reproSteps").GetString()!.Length);
    }

    // --- US-1 ------------------------------------------------------------------------------------------------------

    // US-1 · "Several new fields are saved in one save".
    [Fact]
    public async Task Save_SeveralNewFieldsOfAStory_InOneSave_AreKept()
    {
        var (world, story) = await ItemAsync("US");

        await SavedAsync(world, story, new { storyPoints = new { value = 5 }, risk = new { value = 2 }, acceptanceCriteria = "Вход без пароля" });

        Assert.Equal(
            "storyPoints=5; risk=2; acceptanceCriteria=\"Вход без пароля\"",
            Raw(await WorkItemsTestData.ReadAsync(factory, world, story), "storyPoints", "risk", "acceptanceCriteria"));
    }

    // US-1 · "A save sends only the fields the user changed": A's remaining work and B's activity both stand.
    [Fact]
    public async Task Save_TwoSavesOfDifferentFields_BothStand()
    {
        var (world, task) = await ItemAsync("TASK");

        await SavedAsync(world, task, new { remainingWork = new { value = 4 } });
        await SavedAsync(world, task, new { activity = new { value = "Testing" } });

        Assert.Equal(
            "remainingWork=4; activity=\"Testing\"",
            Raw(await WorkItemsTestData.ReadAsync(factory, world, task), "remainingWork", "activity"));
    }

    // US-1 · "One refused value stores nothing" (BR-M3-38): the business value's text, and the risk sent with it not stored.
    [Fact]
    public async Task Save_OneRefusedValue_StoresNothing()
    {
        var (world, feature) = await ItemAsync("FEAT");

        var answer = await WorkItemsTestData.SaveAsync(factory, world, feature, new { risk = new { value = 1 }, businessValue = new { value = 10_000 } });

        AssertReasons(answer, ("businessValue", OutOfRange, OutOfRangeText));
        Assert.Equal("risk=null", Raw(await WorkItemsTestData.ReadAsync(factory, world, feature), "risk"));
    }

    // mod-003 § 0.4 with feat-022 § 3.2: the new reasons join mod-003's in one answer, every field at once.
    [Fact]
    public async Task Save_NewAndOldReasons_ComeInOneAnswer()
    {
        var (world, bug) = await ItemAsync("BUG");

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new
        {
            title = "",
            severity = new { value = 7 },
            remainingWork = new { value = -2 },
            valueArea = new { value = "Business" },
        });

        AssertReasons(
            answer,
            ("title", "workitems.workitem.title_required", "Введите заголовок."),
            ("severity", NotInList, NotInListText),
            ("remainingWork", OutOfRange, OutOfRangeText),
            ("valueArea", NotOnType, NotOnTypeText));
    }

    // US-1 · "Nothing to save" (BR-M3-38): 2.50 against a stored 2.5, the current values re-sent, an empty field emptied
    // again — 200, «Изменён» unchanged.
    [Fact]
    public async Task Save_EqualValues_ChangeNothing()
    {
        var (world, story) = await ItemAsync("US");
        await SavedAsync(world, story, new { storyPoints = new { value = 2.5m } });
        var before = await WorkItemsTestData.ReadAsync(factory, world, story);

        var same = await SavedAsync(world, story, new
        {
            storyPoints = new { value = 2.50m },
            priority = new { value = 2 },
            risk = new { value = (int?)null },
            valueArea = new { value = "Business" },
            acceptanceCriteria = "",
        });

        Assert.Equal(before.GetProperty("changedAt").GetDateTimeOffset(), same.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world, story)).GetRawText());
    }

    // api-contract § 3.2, "How a field is sent, emptied or left alone": absent or a top-level null leaves a field as it is;
    // `{}` and `{ "value": null }` empty it; a text is emptied with "" and left alone by null.
    [Fact]
    public async Task Save_NullOrAbsentLeavesAField_TheWrapperEmptiesIt()
    {
        var (world, bug) = await ItemAsync("BUG");
        await SavedAsync(world, bug, new { storyPoints = new { value = 3 }, activity = new { value = "Design" }, reproSteps = "Шаги" });

        var untouched = await SavedAsync(world, bug, new { storyPoints = (object?)null, activity = (object?)null, reproSteps = (string?)null, title = "Иное" });
        Assert.Equal(
            "storyPoints=3; activity=\"Design\"; reproSteps=\"Шаги\"",
            Raw(untouched, "storyPoints", "activity", "reproSteps"));

        var emptied = await SavedAsync(world, bug, new { storyPoints = new { }, activity = new { value = (string?)null }, reproSteps = "" });
        Assert.Equal(
            "storyPoints=null; activity=null; reproSteps=\"\"",
            Raw(emptied, "storyPoints", "activity", "reproSteps"));
        Assert.True(emptied.GetProperty("changedAt").GetDateTimeOffset() > untouched.GetProperty("changedAt").GetDateTimeOffset());
    }
}
